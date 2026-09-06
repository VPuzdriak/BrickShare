terraform {
  required_version = ">= 1.15.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
  }

  backend "azurerm" {
    resource_group_name  = "rg-brickshare-tfstate"
    storage_account_name = "stbricksharetfstate"
    container_name       = "tfstate"
    key                  = "catalog.tfstate"
    use_azuread_auth     = true
  }
}

provider "azurerm" {
  features {}
}

data "azurerm_client_config" "current" {}

variable "image_tag" {
  description = "Container image tag to deploy — the commit SHA the pipeline built."
  type        = string
}

variable "postgres_admin_object_id" {
  description = "Object id of the Entra group that administers the Postgres server."
  type        = string
}

variable "postgres_admin_name" {
  description = "Display name of that group. Postgres uses it as the login role name, so it must match Entra exactly."
  type        = string
}

variable "developer_ip" {
  description = "Public IP allowed through the Postgres firewall for the step 8 bootstrap. Null in CI."
  type        = string
  default     = null
}

locals {
  catalog_app_name = "app-brickshare-catalog-dev"
}

resource "azurerm_resource_group" "main" {
  name     = "rg-brickshare-dev"
  location = "westeurope"
}

resource "azurerm_container_registry" "main" {
  name                = "crbrickshare"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = "Basic"
  admin_enabled       = false
}

resource "azurerm_postgresql_flexible_server" "catalog" {
  name                = "psql-brickshare-catalog-dev"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location

  version               = "18"
  sku_name              = "B_Standard_B1ms"
  storage_mb            = 32768
  backup_retention_days = 7
  zone                  = "1"

  authentication {
    active_directory_auth_enabled = true
    password_auth_enabled         = false
  }
}

resource "azurerm_postgresql_flexible_server_active_directory_administrator" "catalog" {
  server_name         = azurerm_postgresql_flexible_server.catalog.name
  resource_group_name = azurerm_resource_group.main.name
  tenant_id           = data.azurerm_client_config.current.tenant_id
  object_id           = var.postgres_admin_object_id
  principal_name      = var.postgres_admin_name
  principal_type      = "Group"
}

resource "azurerm_postgresql_flexible_server_database" "catalog" {
  name      = "brickshare_catalog"
  server_id = azurerm_postgresql_flexible_server.catalog.id
  collation = "en_US.utf8"
  charset   = "utf8"
}

resource "azurerm_postgresql_flexible_server_firewall_rule" "azure_services" {
  name             = "AllowAzureServices"
  server_id        = azurerm_postgresql_flexible_server.catalog.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

resource "azurerm_postgresql_flexible_server_firewall_rule" "developer" {
  count = var.developer_ip == null ? 0 : 1

  name             = "developer"
  server_id        = azurerm_postgresql_flexible_server.catalog.id
  start_ip_address = var.developer_ip
  end_ip_address   = var.developer_ip
}

resource "azurerm_service_plan" "catalog" {
  name                = "plan-brickshare-catalog-dev"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  os_type             = "Linux"
  sku_name            = "B1"
}

resource "azurerm_linux_web_app" "catalog" {
  name                = local.catalog_app_name
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  service_plan_id     = azurerm_service_plan.catalog.id

  identity {
    type = "SystemAssigned"
  }

  site_config {
    container_registry_use_managed_identity = true

    application_stack {
      docker_image_name   = "brickshare-catalog-api:${var.image_tag}"
      docker_registry_url = "https://${azurerm_container_registry.main.login_server}"
    }
  }

  app_settings = {
    WEBSITES_PORT              = "8080"
    ASPNETCORE_ENVIRONMENT     = "Production"
    ConnectionStrings__Catalog = "Host=${azurerm_postgresql_flexible_server.catalog.fqdn};Port=5432;Database=${azurerm_postgresql_flexible_server_database.catalog.name};Username=${local.catalog_app_name};SSL Mode=Require"
  }
}

resource "azurerm_role_assignment" "acr_pull" {
  scope                = azurerm_container_registry.main.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_linux_web_app.catalog.identity[0].principal_id
}

output "web_app_url" {
  value = "https://${azurerm_linux_web_app.catalog.default_hostname}"
}
