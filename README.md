# ELearning ToanHocHay - Backend API

## 📌 Overview

This project is a backend API for an e-learning math platform that supports students and parents in learning and tracking progress. The system integrates AI-powered features to enhance learning effectiveness and provide personalized recommendations.

Built with **ASP.NET Core**, **Entity Framework Core**, and integrated **AI services**.

---

## 🚀 Key Features

### 🔐 Authentication & Authorization

* JWT-based authentication
* Role-based access (Student, Parent)

### 📚 Learning System

* Course, chapter, topic, and lesson management
* Exercise and question system
* Student progress tracking

### 🤖 AI-Powered Features (My Contribution)

* **AI Answer Hint Generation**: Suggests hints for students during practice
* **AI Feedback System**: Provides explanations and feedback for incorrect answers
* **Performance Analysis**: Analyzes student strengths and weaknesses based on test history
* **Personalized Learning Path**: Recommends study roadmap based on performance
* **AI Chatbot**: Supports students and parents with learning guidance

### 💳 Payment & Subscription

* Subscription management
* Payment integration (SePay)

### 📊 Dashboard & Analytics

* Learning progress tracking
* Performance statistics

---

## 🛠️ Tech Stack

* **Backend**: ASP.NET Core Web API
* **Database**: PostgreSQL
* **ORM**: Entity Framework Core
* **Authentication**: JWT
* **AI Integration**: Python services / external AI APIs
* **Deployment**: Docker

---

## ⚙️ Setup & Installation

### 1. Clone repository

```bash
git clone https://github.com/Datnthe11/ELearning_ToanHocHay_Backend.git
cd ELearning_ToanHocHay_Backend
```

### 2. Configure environment variables

Create environment variables for:

* Database connection string
* JWT Secret Key
* External API keys (if any)

Example:

```
DATABASE_URL=your_database_url
JwtSettings__SecretKey=your_secret_key
```

### 3. Run migration

```bash
dotnet ef database update
```

### 4. Run project

```bash
dotnet run
```

---

## 🔐 Security Notice

Sensitive information (e.g., database connection strings, JWT secret keys, API keys) has been removed and is managed via environment variables.

---

## 👨‍💻 My Role

* Developed and integrated AI-driven features into the system
* Designed AI services for:

  * Answer suggestion
  * Feedback generation
  * Learning analytics
  * Chatbot interaction
* Collaborated with team members on backend architecture and API development

---

## 📌 Repository Purpose

This repository is a personal copy of a team project for portfolio and learning purposes.

---

## 📞 Contact

Feel free to connect or reach out for collaboration opportunities.
