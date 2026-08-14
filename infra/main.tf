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
  
  identity {
    type = "SystemAssigned"
  }

  site_config {
    container_registry_use_managed_identity = true

    application_stack {
      docker_image_name   = "brickshare-catalog-api:episode-8"
      docker_registry_url = "https://${azurerm_container_registry.main.login_server}"
    }
  }

  app_settings = {
    WEBSITES_PORT          = "8080"
    ASPNETCORE_ENVIRONMENT = "Production"
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
