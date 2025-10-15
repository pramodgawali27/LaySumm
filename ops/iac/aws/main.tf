# Terraform stub for AWS deployment of LaySumm PLS platform.
# Provisions VPC, EKS/ECS Fargate, S3, OpenSearch, SQS/SNS, Secrets Manager, and Bedrock/OpenAI access.

terraform {
  required_version = ">= 1.5.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.45"
    }
  }
}

provider "aws" {
  region = var.region
}

variable "environment" { type = string }
variable "region" { type = string }
variable "name_prefix" { type = string }

# Networking
module "vpc" {
  source  = "terraform-aws-modules/vpc/aws"
  name    = "${var.name_prefix}-${var.environment}-vpc"
  cidr    = "10.20.0.0/16"
  azs     = slice(data.aws_availability_zones.available.names, 0, 3)
  private_subnets = ["10.20.1.0/24", "10.20.2.0/24", "10.20.3.0/24"]
  public_subnets  = ["10.20.101.0/24", "10.20.102.0/24", "10.20.103.0/24"]
  enable_dns_hostnames = true
  enable_nat_gateway   = true
  single_nat_gateway   = true
  tags = {
    Environment = var.environment
    Project     = var.name_prefix
  }
}

data "aws_availability_zones" "available" {}

# ECS Fargate cluster (swap to EKS if desired)
resource "aws_ecs_cluster" "app" {
  name = "${var.name_prefix}-${var.environment}-cluster"
  setting {
    name  = "containerInsights"
    value = "enabled"
  }
}

# S3 for document storage
resource "aws_s3_bucket" "documents" {
  bucket = lower("${var.name_prefix}-${var.environment}-docs")
  force_destroy = false
  object_lock_enabled = false
  tags = {
    Environment = var.environment
    Project     = var.name_prefix
  }
}

# SQS queues for ingestion/batch with DLQ
resource "aws_sqs_queue" "ingestion" {
  name = "${var.name_prefix}-${var.environment}-ingestion"
  visibility_timeout_seconds = 300
  message_retention_seconds  = 1209600
}

resource "aws_sqs_queue" "ingestion_dlq" {
  name = "${var.name_prefix}-${var.environment}-ingestion-dlq"
  message_retention_seconds = 1209600
}

resource "aws_sqs_queue_redrive_allow_policy" "ingestion" {
  queue_url = aws_sqs_queue.ingestion.url
  redrive_allow_policy = jsonencode({
    redrivePermission = "byQueue"
    sourceQueueArns   = [aws_sqs_queue.ingestion_dlq.arn]
  })
}

# OpenSearch for hybrid retrieval
resource "aws_opensearch_domain" "retrieval" {
  domain_name           = "${var.name_prefix}-${var.environment}-search"
  engine_version        = "OpenSearch_2.13"
  cluster_config {
    instance_type  = "m6g.large.search"
    instance_count = 2
    zone_awareness_enabled = true
  }
  ebs_options {
    ebs_enabled = true
    volume_size = 100
    volume_type = "gp3"
  }
  encrypt_at_rest {
    enabled = true
  }
  node_to_node_encryption {
    enabled = true
  }
  domain_endpoint_options {
    enforce_https = true
  }
}

# Secrets Manager + KMS key for PII maps
resource "aws_kms_key" "pii" {
  description             = "PII redaction map encryption"
  deletion_window_in_days = 30
}

resource "aws_secretsmanager_secret" "pii" {
  name = "${var.name_prefix}/${var.environment}/pii"
  kms_key_id = aws_kms_key.pii.arn
}

# CloudWatch log group for OTLP collector (agent forwards traces/metrics)
resource "aws_cloudwatch_log_group" "otlp" {
  name = "/aws/laySumm/${var.environment}/otlp"
  retention_in_days = 30
}

# Outputs used by GitHub Actions/CD pipelines
output "vpc_id" { value = module.vpc.vpc_id }
output "private_subnet_ids" { value = module.vpc.private_subnets }
output "ecs_cluster_name" { value = aws_ecs_cluster.app.name }
output "documents_bucket" { value = aws_s3_bucket.documents.bucket }
output "ingestion_queue_url" { value = aws_sqs_queue.ingestion.url }
output "opensearch_endpoint" { value = aws_opensearch_domain.retrieval.endpoint }
