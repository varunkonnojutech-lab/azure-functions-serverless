# Azure Functions Serverless — Order Processing

[![Build](https://github.com/varunkonnojutech-lab/azure-functions-serverless/actions/workflows/ci.yml/badge.svg)](https://github.com/varunkonnojutech-lab/azure-functions-serverless/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Azure Functions](https://img.shields.io/badge/Azure-Functions-0078D4)](https://learn.microsoft.com/azure/azure-functions/)
[![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/varunkonnojutech-lab/azure-functions-serverless)

A serverless order-processing backend on **.NET 8 (isolated worker model)**: an HTTP
API for orders, a Service Bus–triggered fulfillment processor, and a scheduled
daily report — with infrastructure as code (Bicep) and Key Vault secret
references, the way enterprise Azure workloads are actually built.

## Who is this for

Backend developers moving to the cloud who want to see how serverless *really*
looks in production: not just a "hello world" function, but triggers, messaging,
scheduled jobs, secret management, and the infrastructure to host it all —
written the way enterprise teams do it.

## What you'll learn

- **Azure Functions triggers**: HTTP, Service Bus, and Timer — the three you'll use most
- **Isolated worker model**: plain classes with constructor injection (`ILogger<T>`, services) — no statics required
- **Messaging**: decoupling work with a Service Bus queue between API and processor
- **Key Vault references**: keeping secrets out of app settings and source control
- **Infrastructure as code**: the full environment (storage, messaging, vault, monitoring) in one Bicep file

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local) (`func`)
- [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) (local storage emulator) — or a real storage connection string
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) (for deployment)

## Quick start

```bash
git clone https://github.com/varunkonnojutech-lab/azure-functions-serverless.git
cd azure-functions-serverless
```

(Codespaces users: click the badge above, then continue from step 1 below.)

```bash
# 1. Copy the sample settings and fill in your Service Bus connection string
#    (or leave the placeholder — the HTTP and timer functions run without it)
cd src/OrderFunctions
cp local.settings.json.sample local.settings.json

# 2. Start Azurite (in another terminal)
azurite

# 3. Build and run
dotnet build
func start
```

Try it:

```bash
curl -X POST http://localhost:7071/api/orders \
  -H "Content-Type: application/json" \
  -d '{"customerName":"Acme Corp","total":199.99}'

curl http://localhost:7071/api/orders
```

To exercise `ProcessOrder` locally, drop a message on the `orders` queue of a
Service Bus namespace and point `ServiceBusConnection` at its connection string.

## How it works

```
                    +------------------+
  POST /api/orders  |  OrderApi        |      +-------------------+
 -----------------> |  (HTTP trigger)  |----> | InMemoryOrderStore  |
                    +------------------+      +-------------------+
  GET /api/orders                                            |
  GET /api/orders/{id}                                       | (production: publish
                                                             |  to Service Bus here)
                    +------------------+      +-------------------+
  "orders" queue    |  OrderProcessor  |<-----| Azure Service Bus   |
 -----------------> |  (SB trigger)    |      +-------------------+
                    +------------------+

                    +------------------+
  9:00 AM daily     |  DailyReport     |
 -----------------> |  (Timer trigger) |
                    +------------------+
```

| Function | Trigger | Route / Schedule | What it does |
|---|---|---|---|
| `CreateOrder` | HTTP `POST` | `/api/orders` | Validates the payload, stores the order, returns `201` with a `Location` header |
| `GetOrders` | HTTP `GET` | `/api/orders` | Lists orders, newest first |
| `GetOrder` | HTTP `GET` | `/api/orders/{id}` | Returns one order or `404` |
| `ProcessOrder` | Service Bus | queue `orders` | Simulated fulfillment: payment capture, inventory reservation, confirmation |
| `DailyReport` | Timer | `0 0 9 * * *` (9 AM daily) | Logs an order-count summary |

The HTTP API is the front door, the Service Bus queue decouples order intake
from fulfillment (so a slow payment provider never blocks your API), and the
timer keeps a daily pulse on the system. The isolated worker model means your
function classes are ordinary classes — constructor injection, no statics.

## Deploy

```bash
# 1. Create the resource group and deploy the infrastructure
az group create --name orders-rg --location eastus
az deployment group create \
  --resource-group orders-rg \
  --template-file infra/main.bicep \
  --parameters projectName=orders

# 2. Publish the Function App (name is printed as the functionAppName output)
func azure functionapp publish <function-app-name>
```

### What `infra/main.bicep` creates

- Storage account (Functions host storage)
- Service Bus namespace (Standard) + `orders` queue
- Log Analytics workspace + Application Insights
- Key Vault holding the Service Bus connection string as a secret
- Linux Consumption plan (Y1) + Function App (`.NET 8 isolated`)

### Key Vault reference

The Function App never stores the Service Bus connection string in plain text.
The `ServiceBusConnection` app setting is a
[Key Vault reference](https://learn.microsoft.com/azure/app-service/app-service-key-vault-references):

```
@Microsoft.KeyVault(SecretUri=https://<vault>.vault.azure.net/secrets/ServiceBusConnection/<version>)
```

At runtime the Functions host resolves it using the app's system-assigned
managed identity, which the Bicep template grants `Get`/`List` on secrets.
No secrets in app settings, no secrets in source control.

## Project structure

```
src/OrderFunctions/
  Program.cs                 # Host builder, DI, Application Insights wiring
  host.json                  # Logging + Application Insights sampling
  local.settings.json.sample # Local settings template (real file is gitignored)
  Models/Order.cs            # OrderRequest / Order records
  Services/
    IOrderStore.cs           # Storage abstraction
    InMemoryOrderStore.cs    # Thread-safe in-memory implementation
  Functions/
    OrderApi.cs              # CreateOrder / GetOrders / GetOrder (HTTP)
    OrderProcessor.cs        # ProcessOrder (Service Bus trigger)
    DailyReport.cs           # DailyReport (timer trigger)
infra/
  main.bicep                 # Full Azure infrastructure
.devcontainer/               # One-click Codespaces setup
```

## Notes

- The isolated worker model runs your code in a separate .NET process; function
  classes are plain classes with constructor injection (`ILogger<T>`,
  `IOrderStore`) — no static methods required.
- `host.json` has no `extensionBundles` section: bundles are a script-model
  concept and are not needed (or used) by the isolated worker.
- `InMemoryOrderStore` is registered as a singleton; swap the registration in
  `Program.cs` for Cosmos DB or SQL Server in production without touching the
  function code.

## Next steps

Continue the learning path with [terraform-azure](https://github.com/varunkonnojutech-lab/terraform-azure) — manage the *same kind* of Azure infrastructure (web app, SQL, Key Vault, Service Bus) as versioned, reviewable code with Terraform.

## License

MIT — see [LICENSE](LICENSE).
