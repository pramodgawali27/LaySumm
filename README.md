# 🧠 LaySumm: Research on Simplified Summarization with LLMs

[![MIT License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)  
**LaySumm** is a research project focused on generating **Plain Language Summaries (PLS)** from complex documents (e.g., medical, scientific, or legal) using Large Language Models (LLMs) like GPT-4, LLaMA, Mistral, and others. This project explores **prompt engineering**, **fine-tuning**, and **retrieval-augmented generation (RAG)** strategies to produce summaries that are **readable**, **factually accurate**, and **accessible** to general audiences.

---

## 🎯 Project Goals

- ✅ Build reproducible pipelines for generating simplified summaries using LLMs.
- ✅ Evaluate readability (e.g., Flesch-Kincaid) and factual consistency of generated content.
- ✅ Compare OpenAI, Hugging Face, and open-source models for PLS tasks.
- ✅ Explore prompt-tuning, fine-tuning, and retrieval-based summarization.
- ✅ Provide dashboards and metrics to visualize improvement over baselines.

---

## 🧱 Repository Structure

```bash
LaySumm/
├── data/                  # Input and output datasets
│   ├── raw/               # Raw input documents
│   ├── processed/         # Preprocessed input/output pairs
│   ├── prompts/           # Prompt templates
│   └── datasets.md        # Documentation for datasets
│
├── models/
│   ├── baselines/         # Prompt-only summarizers (e.g., GPT-4)
│   ├── fine-tuned/        # Fine-tuned checkpoints
│   └── retriever/         # RAG components (e.g., FAISS, Azure Search)
│
├── evaluation/
│   ├── metrics.py         # ROUGE, BERTScore, readability metrics
│   ├── factual_eval.py    # QA-based or LLM-based factuality scoring
│   ├── results/           # Evaluation output data
│   └── plots/             # Visualization (charts, graphs)
│
├── notebooks/             # Jupyter Notebooks for experimentation
│   ├── 01_generate_summary.ipynb
│   ├── 02_fine_tuning.ipynb
│   └── 03_evaluation_dashboard.ipynb
│
├── src/
│   ├── pipeline.py        # End-to-end summarization pipeline
│   ├── inference.py       # Model runner
│   └── retriever.py       # For RAG use case
│
├── docs/                  # Project documentation
│   ├── architecture.md
│   ├── evaluation-methods.md
│   └── model-notes.md
│
├── research-paper.md      # Extended summary of research or whitepaper
├── requirements.txt       # Python dependencies
├── LICENSE                # MIT License
├── .gitignore             # Ignore files (e.g., checkpoints, env)
└── README.md              # You're here!
