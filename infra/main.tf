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

variable "docker_image" {
  description = "Docker Hub image reference for the catalog API, e.g. \"yourname/brickshare-catalog-api:episode-6\"."
  type        = string
}

resource "azurerm_resource_group" "main" {
  name     = "rg-brickshare-dev"
  location = "westeurope"
}

resource "azurerm_service_plan" "catalog" {
  name                = "plan-brickshare-catalog-dev"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  os_type             = "Linux"
  sku_name            = "B1"
}

resource "azurerm_linux_web_app" "catalog" {
  name                = "app-brickshare-catalog-dev"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  service_plan_id     = azurerm_service_plan.catalog.id

  site_config {
    application_stack {
      docker_image_name   = var.docker_image
      docker_registry_url = "https://index.docker.io"
    }
  }

  app_settings = {
    WEBSITES_PORT          = "8080"
    ASPNETCORE_ENVIRONMENT = "Production"
  }
}

output "web_app_url" {
  value = "https://${azurerm_linux_web_app.catalog.default_hostname}"
}