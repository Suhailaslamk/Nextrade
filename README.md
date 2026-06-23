# NexTrade

> A distributed trading platform built with .NET 8, Microservices, Kafka, gRPC, CQRS, and Event-Driven Architecture.

![.NET](https://img.shields.io/badge/.NET-8.0-blue)
![Kafka](https://img.shields.io/badge/Kafka-Event%20Streaming-black)
![SQL%20Server](https://img.shields.io/badge/SQL%20Server-Database-red)
![Redis](https://img.shields.io/badge/Redis-Caching-red)
![gRPC](https://img.shields.io/badge/gRPC-Communication-green)
![CQRS](https://img.shields.io/badge/CQRS-MediatR-purple)

## Overview

NexTrade is a high-performance distributed trading platform designed to explore real-world financial system architecture using modern backend engineering practices.

The project focuses on:

* Event-Driven Architecture
* CQRS + MediatR
* Kafka-based Messaging
* High-Performance Matching Engine
* gRPC Service Communication
* JWT Authentication
* Distributed System Design
* Observability and Scalability Patterns

The goal is to simulate the architectural foundations commonly found in modern brokerage, exchange, and fintech systems while maintaining clean separation of concerns across independently deployable services.

---

# Current Project Status

### Implemented Services

| Service         | Status      |
| --------------- | ----------- |
| Auth Service    | ✅ Completed |
| Trading Service | ✅ Completed |
| Matching Engine | ✅ Completed |

### Planned Services

| Service              | Status     |
| -------------------- | ---------- |
| Settlement Service   | 🚧 Planned |
| Portfolio Service    | 🚧 Planned |
| Market Data Service  | 🚧 Planned |
| Notification Service | 🚧 Planned |
| GraphQL Gateway      | 🚧 Planned |

---

# Architecture Overview

```text
Client
  │
  ▼
API Gateway
  │
  ├──────────────► Auth Service
  │
  └──────────────► Trading Service
                          │
                          │ gRPC
                          ▼
                    Risk Validation
                          │
                          ▼
                   SQL Server
                          │
                          ▼
                 Kafka Event Bus
                          │
                          ▼
                  Matching Engine
                          │
                          ▼
                 Trade Events
```

## Service Responsibilities

### Auth Service

Handles identity and access management.

Responsibilities:

* User Registration
* User Login
* Refresh Tokens
* JWT Generation
* Token Validation
* Role-Based Authorization

Technologies:

* ASP.NET Core
* Entity Framework Core
* SQL Server
* JWT (RS256)
* Redis Token Revocation

---

### Trading Service

Responsible for the complete order lifecycle.

Responsibilities:

* Submit Orders
* Cancel Orders
* Order Validation
* Balance Checks
* Order Persistence
* Event Publishing

Architectural Patterns:

* CQRS
* MediatR
* Transactional Outbox
* FluentValidation
* Optimistic Concurrency

Commands:

* SubmitOrderCommand
* CancelOrderCommand

Queries:

* GetOrderByIdQuery
* GetOrdersQuery

---

### Matching Engine

The Matching Engine is the core trading component responsible for matching buy and sell orders using a price-time priority model.

Supported Features:

* Market Orders
* Limit Orders
* Partial Fills
* Order Cancellation
* Self-Match Prevention
* FIFO Matching

Core Data Structures:

* SortedDictionary
* LinkedList
* Dictionary Index

Performance Goals:

* Low Latency Matching
* O(1) Queue Operations
* O(log n) Price Level Operations
* Lock-Free Symbol Processing

---

# Service Communication

NexTrade uses both synchronous and asynchronous communication patterns.

## Synchronous Communication

### gRPC

| Caller          | Callee       | Purpose             |
| --------------- | ------------ | ------------------- |
| Gateway         | Auth Service | JWT Validation      |
| Trading Service | Risk Service | Risk Verification   |
| Trading Service | Risk Service | Balance Reservation |

---

## Asynchronous Communication

Kafka is used for event-driven workflows.

```text
Trading Service
       │
       ▼
orders.submitted
       │
       ▼
Matching Engine
       │
       ▼
trades.executed
```

Kafka provides:

* Decoupled Services
* Reliable Delivery
* Horizontal Scalability
* Event Replay
* Fault Tolerance

---

# Order Lifecycle

1. User submits an order.
2. Trading Service validates the request.
3. Risk validation is performed.
4. Funds are reserved.
5. Order is stored.
6. OrderSubmitted event is published.
7. Matching Engine consumes the event.
8. Matching logic executes.
9. TradeExecuted event is published.
10. Downstream services consume trade events.

---

# CQRS Architecture

Each service follows a CQRS architecture powered by MediatR.

```text
Controller
    │
    ▼
MediatR
    │
    ├── Validation Behavior
    ├── Logging Behavior
    ├── Transaction Behavior
    ├── Idempotency Behavior
    └── Metrics Behavior
    │
    ▼
Command / Query Handler
    │
    ▼
Persistence Layer
```

Benefits:

* Clear separation of reads and writes
* Easier testing
* Better maintainability
* Flexible scaling strategy

---

# Security

### Authentication

* JWT Authentication
* RS256 Signing
* Refresh Tokens
* Token Revocation

### Authorization

* Role-Based Access Control
* Policy-Based Authorization

### Infrastructure Security

* Rate Limiting
* Kafka ACLs
* Redis Token Blacklist
* Secure Secret Management

---

# Observability

NexTrade includes built-in observability support.

### Logging

* Serilog
* Structured Logging
* Correlation IDs

### Tracing

* OpenTelemetry
* Distributed Tracing

### Metrics

* Prometheus
* Grafana Dashboards

Key Metrics:

* Orders Submitted
* Trades Executed
* Matching Latency
* Consumer Lag
* Error Rates

---

# Technology Stack

### Backend

* .NET 8
* ASP.NET Core
* Entity Framework Core
* MediatR
* FluentValidation

### Messaging

* Apache Kafka

### Database

* SQL Server

### Cache

* Redis

### Communication

* gRPC

### Observability

* OpenTelemetry
* Prometheus
* Grafana
* Jaeger
* Serilog

---

# Roadmap

### Phase 1

* [x] Auth Service
* [x] Trading Service
* [x] Matching Engine

### Phase 2

* [ ] Settlement Service
* [ ] Portfolio Service

### Phase 3

* [ ] Market Data Service
* [ ] Notification Service

### Phase 4

* [ ] GraphQL Gateway
* [ ] Kubernetes Deployment
* [ ] Full End-to-End Trading Flow

---

# Learning Objectives

This project was built to explore:

* Distributed Systems
* Event-Driven Architecture
* Financial System Design
* High-Performance Backend Engineering
* Microservices
* CQRS
* Kafka Messaging
* Scalable System Design
