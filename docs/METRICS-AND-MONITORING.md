# Metrics and Monitoring Guide

This guide shows how to monitor your LLM backend usage with Prometheus and Grafana, including request rates, token usage, estimated costs, and error tracking.

## Table of Contents
- [Overview](#overview)
- [Quick Start](#quick-start)
- [Available Metrics](#available-metrics)
- [Configuration](#configuration)
- [Prometheus Setup](#prometheus-setup)
- [Grafana Setup](#grafana-setup)
- [Example Queries](#example-queries)
- [Sample Dashboard](#sample-dashboard)
- [Best Practices](#best-practices)

## Overview

The mostlylucid.llmbackend library provides built-in Prometheus metrics for monitoring:

- **Request metrics** - Total requests, success/failure rates, durations
- **Token usage** - Prompt, completion, and total tokens per backend/model
- **Cost tracking** - Estimated costs based on configured pricing
- **Budget tracking** (NEW!) - Current spend vs limits with automatic backend disabling
- **Error tracking** - Categorized errors (rate limits, timeouts, auth, etc.)
- **Health monitoring** - Backend availability and status
- **Concurrency** - Active requests in flight

All metrics are labeled by backend name and model, allowing fine-grained monitoring and cost attribution.

## Quick Start

### 1. Enable Metrics in Configuration

```json
{
  "LlmSettings": {
    "Telemetry": {
      "EnableMetrics": true,
      "LogTokenCounts": true
    },
    "Backends": [
      {
        "Name": "openai-primary",
        "Type": "OpenAI",
        "ModelName": "gpt-4",
        "CostPerMillionInputTokens": 30.00,
        "CostPerMillionOutputTokens": 60.00
      }
    ]
  }
}
```

### 2. Add Metrics Endpoint to Your Application

#### ASP.NET Core (Minimal API)

```csharp
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Add LLM backend services
builder.Services.AddLlmBackend(builder.Configuration);

var app = builder.Build();

// Expose Prometheus metrics at /metrics endpoint
app.MapMetrics();

app.Run();
```

#### ASP.NET Core (MVC/Controllers)

```csharp
using Prometheus;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers();
        services.AddLlmBackend(Configuration);
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapMetrics(); // Exposes /metrics
        });
    }
}
```

### 3. Test Metrics Endpoint

```bash
curl http://localhost:5000/metrics
```

You should see Prometheus-formatted metrics like:

```
# HELP llm_requests_total Total number of LLM requests
# TYPE llm_requests_total counter
llm_requests_total{backend="openai-primary",model="gpt-4",status="success"} 42

# HELP llm_estimated_cost_usd Estimated cost in USD
# TYPE llm_estimated_cost_usd counter
llm_estimated_cost_usd{backend="openai-primary",model="gpt-4"} 0.125
```

## Available Metrics

### llm_requests_total (Counter)

Total number of LLM requests.

**Labels:**
- `backend` - Backend name (e.g., "openai-primary")
- `model` - Model name (e.g., "gpt-4")
- `status` - Request status ("success", "failure", "timeout", "cancelled")

**Use cases:**
- Request rate monitoring
- Success/failure ratio
- Traffic distribution across backends

### llm_request_duration_seconds (Histogram)

Request latency distribution in seconds.

**Labels:**
- `backend` - Backend name
- `model` - Model name

**Buckets:** 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 20, 30, 60 seconds

**Use cases:**
- P50/P95/P99 latency tracking
- SLA monitoring
- Performance regression detection

### llm_tokens_total (Counter)

Total token usage.

**Labels:**
- `backend` - Backend name
- `model` - Model name
- `token_type` - Token type ("prompt", "completion", "total")

**Use cases:**
- Token usage tracking
- Cost forecasting
- Quota management

### llm_estimated_cost_usd (Counter)

Estimated cumulative cost in USD.

**Labels:**
- `backend` - Backend name
- `model` - Model name

**Note:** Based on `CostPerMillionInputTokens` and `CostPerMillionOutputTokens` configuration. Always verify with actual provider billing.

**Use cases:**
- Cost monitoring
- Budget alerts
- Per-backend cost attribution

### llm_errors_total (Counter)

Total number of errors.

**Labels:**
- `backend` - Backend name
- `error_type` - Error category:
  - `api_error` - HTTP 4xx/5xx errors
  - `timeout` - Request timeouts
  - `rate_limit` - Rate limit/quota exceeded
  - `auth` - Authentication failures
  - `network` - Network/connection errors
  - `unknown` - Uncategorized errors

**Use cases:**
- Error rate monitoring
- Alert on specific error types
- Diagnose backend issues

### llm_backend_health (Gauge)

Backend health status (1=healthy, 0=unhealthy).

**Labels:**
- `backend` - Backend name

**Use cases:**
- Failover monitoring
- Backend availability tracking
- Alert on unhealthy backends

### llm_active_requests (Gauge)

Number of requests currently in flight.

**Labels:**
- `backend` - Backend name

**Use cases:**
- Concurrency monitoring
- Load balancing
- Detect stuck requests

### llm_backend_budget_usd (Gauge) - NEW!

Current spend and budget limit in USD per backend.

**Labels:**
- `backend` - Backend name
- `limit_type` - Either "current" (spend so far) or "max" (configured limit)

**Use cases:**
- Budget monitoring in real-time
- Alert when approaching spend limits
- Visualize current vs max spend
- Track automatic backend disabling

**Example values:**
```
llm_backend_budget_usd{backend="GPT-4o",limit_type="current"} 7.32
llm_backend_budget_usd{backend="GPT-4o",limit_type="max"} 10.00
```

**Configuration:**
```json
{
  "Backends": [{
    "Name": "GPT-4o",
    "MaxSpendUsd": 10.00,
    "SpendResetPeriod": "Daily"
  }]
}
```

When current >= max, backend automatically becomes unavailable until next reset period.

## Configuration

### Cost Tracking

Configure pricing per backend for accurate cost estimation:

```json
{
  "LlmSettings": {
    "Backends": [
      {
        "Name": "openai-gpt4",
        "Type": "OpenAI",
        "ModelName": "gpt-4",
        "CostPerMillionInputTokens": 30.00,
        "CostPerMillionOutputTokens": 60.00
      },
      {
        "Name": "anthropic-claude",
        "Type": "Anthropic",
        "ModelName": "claude-3-5-sonnet-20241022",
        "CostPerMillionInputTokens": 3.00,
        "CostPerMillionOutputTokens": 15.00
      },
      {
        "Name": "local-ollama",
        "Type": "Ollama",
        "ModelName": "llama2",
        "CostPerMillionInputTokens": 0.00,
        "CostPerMillionOutputTokens": 0.00
      }
    ]
  }
}
```

Pricing sources:
- **OpenAI**: https://openai.com/pricing
- **Anthropic**: https://www.anthropic.com/pricing
- **Google Gemini**: https://ai.google.dev/pricing
- **Cohere**: https://cohere.com/pricing

### Metrics Granularity

Control what gets tracked:

```json
{
  "LlmSettings": {
    "Telemetry": {
      "EnableMetrics": true,
      "LogTokenCounts": true,
      "LogTiming": true
    }
  }
}
```

## Prometheus Setup

### 1. Install Prometheus

**Docker:**
```bash
docker run -d \
  -p 9090:9090 \
  -v $(pwd)/prometheus.yml:/etc/prometheus/prometheus.yml \
  prom/prometheus
```

**Native:**
Download from https://prometheus.io/download/

### 2. Configure Prometheus

Create `prometheus.yml`:

```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: 'llm-backend'
    static_configs:
      - targets: ['localhost:5000']  # Your app's /metrics endpoint
    metrics_path: /metrics
```

If running app in Docker, use host gateway:

```yaml
scrape_configs:
  - job_name: 'llm-backend'
    static_configs:
      - targets: ['host.docker.internal:5000']
```

### 3. Start Prometheus

```bash
docker run -d \
  -p 9090:9090 \
  -v $(pwd)/prometheus.yml:/etc/prometheus/prometheus.yml \
  --add-host=host.docker.internal:host-gateway \
  prom/prometheus
```

### 4. Verify Scraping

Visit http://localhost:9090/targets - your endpoint should show as "UP"

## Grafana Setup

### 1. Install Grafana

**Docker:**
```bash
docker run -d \
  -p 3000:3000 \
  --name=grafana \
  grafana/grafana
```

Default credentials: admin/admin

### 2. Add Prometheus Data Source

1. Open Grafana at http://localhost:3000
2. Go to **Configuration** → **Data Sources**
3. Click **Add data source**
4. Select **Prometheus**
5. Set URL: `http://localhost:9090` (or `http://host.docker.internal:9090` if in Docker)
6. Click **Save & Test**

### 3. Create Dashboard

Import the sample dashboard (see below) or create custom panels.

## Example Queries

### Request Rate

Requests per second by backend:
```promql
rate(llm_requests_total{status="success"}[5m])
```

### Success Rate

Percentage of successful requests:
```promql
sum(rate(llm_requests_total{status="success"}[5m]))
/
sum(rate(llm_requests_total[5m])) * 100
```

### Average Latency

Average request duration:
```promql
rate(llm_request_duration_seconds_sum[5m])
/
rate(llm_request_duration_seconds_count[5m])
```

### P95 Latency

95th percentile request duration:
```promql
histogram_quantile(0.95,
  rate(llm_request_duration_seconds_bucket[5m])
)
```

### Tokens per Second

Token throughput by type:
```promql
rate(llm_tokens_total[5m])
```

### Cost per Hour

Estimated hourly cost:
```promql
increase(llm_estimated_cost_usd[1h])
```

### Daily Cost Forecast

Estimated daily cost based on current rate:
```promql
increase(llm_estimated_cost_usd[1h]) * 24
```

### Cost by Backend

Total cost per backend:
```promql
sum by (backend) (llm_estimated_cost_usd)
```

### Error Rate

Errors per second by type:
```promql
rate(llm_errors_total[5m])
```

### Backend Health

Number of healthy backends:
```promql
sum(llm_backend_health)
```

### Active Requests

Current concurrent requests:
```promql
sum(llm_active_requests)
```

### Max Concurrent Requests (Last Hour)

Peak concurrency:
```promql
max_over_time(sum(llm_active_requests)[1h])
```

### Budget Usage Percentage (NEW!)

Current spend as percentage of limit:
```promql
(llm_backend_budget_usd{limit_type="current"}
/
llm_backend_budget_usd{limit_type="max"}) * 100
```

### Backends Near Budget Limit

Backends above 80% of budget:
```promql
llm_backend_budget_usd{limit_type="current"}
/
llm_backend_budget_usd{limit_type="max"} > 0.8
```

### Budget Remaining

Amount left to spend:
```promql
llm_backend_budget_usd{limit_type="max"}
-
llm_backend_budget_usd{limit_type="current"}
```

## Sample Dashboard

Here's a complete Grafana dashboard JSON you can import:

### Dashboard Overview

The sample dashboard includes:

1. **Top Row - Key Metrics**
   - Total Requests (24h)
   - Success Rate (%)
   - Average Latency (P95)
   - Estimated Cost (24h)

2. **Second Row - Request Metrics**
   - Request Rate (time series)
   - Requests by Backend (pie chart)
   - Request Duration (heatmap)

3. **Third Row - Token & Cost**
   - Tokens per Second (stacked)
   - Cost per Hour (by backend)
   - Cumulative Cost (time series)

4. **Fourth Row - Errors & Health**
   - Error Rate (by type)
   - Backend Health Status
   - Active Requests

### Import Instructions

1. In Grafana, go to **Dashboards** → **Import**
2. Create a new dashboard with these panels:

#### Panel 1: Request Rate
```json
{
  "title": "Request Rate",
  "targets": [{
    "expr": "rate(llm_requests_total{status=\"success\"}[5m])"
  }],
  "type": "graph"
}
```

#### Panel 2: Estimated Cost (24h)
```json
{
  "title": "Estimated Cost (24h)",
  "targets": [{
    "expr": "sum(increase(llm_estimated_cost_usd[24h]))"
  }],
  "type": "stat",
  "format": "currencyUSD"
}
```

#### Panel 3: Token Usage
```json
{
  "title": "Tokens per Second",
  "targets": [{
    "expr": "sum by (token_type) (rate(llm_tokens_total[5m]))"
  }],
  "type": "graph",
  "stack": true
}
```

#### Panel 4: P95 Latency
```json
{
  "title": "P95 Latency",
  "targets": [{
    "expr": "histogram_quantile(0.95, rate(llm_request_duration_seconds_bucket[5m]))"
  }],
  "type": "graph"
}
```

## Best Practices

### 1. Set Up Alerts

**High Error Rate Alert:**
```yaml
groups:
  - name: llm_alerts
    rules:
      - alert: HighLLMErrorRate
        expr: |
          rate(llm_errors_total[5m]) > 0.05
        for: 5m
        annotations:
          summary: "High LLM error rate detected"
```

**Cost Alert:**
```yaml
- alert: LLMCostExceeded
  expr: |
    increase(llm_estimated_cost_usd[1h]) > 10
  annotations:
    summary: "LLM costs exceeded $10/hour"
```

**Budget Alert (NEW!):**
```yaml
- alert: LLMBudgetNearLimit
  expr: |
    (llm_backend_budget_usd{limit_type="current"}
    /
    llm_backend_budget_usd{limit_type="max"}) > 0.9
  for: 5m
  annotations:
    summary: "Backend {{ $labels.backend }} has used 90% of budget"
    description: "Current spend: {{ $value }}%"
```

**Budget Exceeded Alert:**
```yaml
- alert: LLMBudgetExceeded
  expr: |
    llm_backend_budget_usd{limit_type="current"}
    >=
    llm_backend_budget_usd{limit_type="max"}
  for: 1m
  annotations:
    summary: "Backend {{ $labels.backend }} budget exceeded - auto-disabled"
    description: "Backend will be re-enabled at next reset period"
```

**Latency Alert:**
```yaml
- alert: HighLLMLatency
  expr: |
    histogram_quantile(0.95, rate(llm_request_duration_seconds_bucket[5m])) > 30
  for: 10m
  annotations:
    summary: "P95 latency above 30s"
```

### 2. Use Recording Rules

Pre-compute expensive queries:

```yaml
groups:
  - name: llm_recording_rules
    interval: 30s
    rules:
      - record: job:llm_request_rate:5m
        expr: rate(llm_requests_total[5m])

      - record: job:llm_success_rate:5m
        expr: |
          sum(rate(llm_requests_total{status="success"}[5m]))
          /
          sum(rate(llm_requests_total[5m]))
```

### 3. Retention and Storage

Configure appropriate retention:

```yaml
# prometheus.yml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

storage:
  tsdb:
    retention.time: 30d
    retention.size: 10GB
```

### 4. Cost Accuracy

- Update pricing regularly
- Cross-reference with actual bills
- Add safety margin for estimates
- Monitor usage trends

### 5. Performance

- Use recording rules for complex queries
- Limit dashboard refresh rate (30s-1m)
- Use appropriate time ranges
- Aggregate when possible

## Troubleshooting

### Metrics Not Appearing

1. **Check /metrics endpoint is accessible:**
   ```bash
   curl http://localhost:5000/metrics | grep llm_
   ```

2. **Verify Prometheus is scraping:**
   - Visit http://localhost:9090/targets
   - Check for errors

3. **Ensure telemetry is enabled:**
   ```json
   {
     "Telemetry": {
       "EnableMetrics": true
     }
   }
   ```

### Cost Estimates Wrong

1. **Verify pricing configuration:**
   - Check `CostPerMillionInputTokens`
   - Check `CostPerMillionOutputTokens`

2. **Confirm token counts are logged:**
   ```json
   {
     "Telemetry": {
       "LogTokenCounts": true
     }
   }
   ```

### High Cardinality Warning

If you see "too many time series" warnings:

1. **Limit model variations** - Avoid dynamic model names
2. **Aggregate in queries** - Use `sum by (backend)` instead of full labels
3. **Drop unused labels** - Configure Prometheus relabeling if needed

## Integration Examples

### With Azure Monitor

Export Prometheus metrics to Azure:

```yaml
remote_write:
  - url: "https://<workspace-id>.azure.com/api/v1/write"
    basic_auth:
      username: <workspace-id>
      password: <access-key>
```

### With AWS CloudWatch

Use Prometheus CloudWatch exporter or AWS Managed Prometheus.

### With DataDog

Install DataDog agent with Prometheus scraping enabled.

## Summary

The mostlylucid.llmbackend library provides comprehensive Prometheus metrics out of the box:

- ✅ **Request metrics** - Rate, duration, success/failure
- ✅ **Token tracking** - Usage by type and backend
- ✅ **Cost estimation** - Real-time cost tracking
- ✅ **Budget tracking** (NEW!) - Automatic spend limits with backend disabling
- ✅ **Error categorization** - Detailed error types
- ✅ **Health monitoring** - Backend availability
- ✅ **Concurrency** - Active request tracking

With this monitoring stack, you can:
- Track LLM usage and costs in real-time
- Set budget limits with automatic enforcement
- Get alerted before and when budgets are exceeded
- Set up alerts for anomalies
- Optimize backend selection
- Plan capacity and budgets
- Debug issues quickly
- Visualize spend vs budget limits in Grafana

For more information:
- Prometheus documentation: https://prometheus.io/docs/
- Grafana documentation: https://grafana.com/docs/
- prometheus-net library: https://github.com/prometheus-net/prometheus-net
