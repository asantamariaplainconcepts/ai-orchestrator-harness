# One Ubuntu VM with nested virtualization, running Docker Sandboxes (sbx): the same CLI as on the
# laptop, inside our own subscription. "Local or cloud" becomes "which machine runs `sbx`".
#
# Every resource is named here, because the tenant's "Allowed nomenclature" policy denies any
# vm/nic/nsg/pip/vnet not shaped <project>-<env>-<abbr>-<NN> — the reason AKS could not be created
# in this subscription (its MC_ group names its own VMSS and NSG). The resource group is read,
# never managed: destroy removes what is below and nothing else.

terraform {
  required_version = ">= 1.9"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 5.7"
    }
  }
}

provider "azurerm" {
  features {}
  subscription_id = var.subscription_id
}

data "azurerm_resource_group" "target" {
  name = var.resource_group_name
}

locals {
  name     = "${var.project}-${var.environment}"
  location = data.azurerm_resource_group.target.location
  rg       = data.azurerm_resource_group.target.name
  tags = merge(var.tags, {
    purpose    = "sbx-on-vm-probe"
    managed_by = "terraform"
  })
}

resource "azurerm_virtual_network" "vnet" {
  name                = "${local.name}-vnet-01"
  location            = local.location
  resource_group_name = local.rg
  address_space       = ["10.42.0.0/24"]
  tags                = local.tags
}

resource "azurerm_subnet" "vms" {
  name                 = "vms"
  resource_group_name  = local.rg
  virtual_network_name = azurerm_virtual_network.vnet.name
  address_prefixes     = ["10.42.0.0/26"]
}

# SSH from one address only — the machine that ran `terraform apply`, unless told otherwise.
resource "azurerm_network_security_group" "nsg" {
  name                = "${local.name}-nsg-01"
  location            = local.location
  resource_group_name = local.rg
  tags                = local.tags

  security_rule {
    name                       = "ssh-from-operator"
    priority                   = 100
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "22"
    source_address_prefix      = var.ssh_source_cidr
    destination_address_prefix = "*"
  }
}

resource "azurerm_public_ip" "pip" {
  name                = "${local.name}-pip-01"
  location            = local.location
  resource_group_name = local.rg
  allocation_method   = "Static"
  sku                 = "Standard"
  tags                = local.tags
}

resource "azurerm_network_interface" "nic" {
  name                = "${local.name}-nic-01"
  location            = local.location
  resource_group_name = local.rg
  tags                = local.tags

  ip_configuration {
    name                          = "primary"
    subnet_id                     = azurerm_subnet.vms.id
    private_ip_address_allocation = "Dynamic"
    public_ip_address_id          = azurerm_public_ip.pip.id
  }
}

resource "azurerm_network_interface_security_group_association" "nic" {
  network_interface_id      = azurerm_network_interface.nic.id
  network_security_group_id = azurerm_network_security_group.nsg.id
}

# Dsv5 is generation 2, x86, and exposes VT-x to the guest — sbx needs /dev/kvm inside the VM,
# and that is what nested virtualization gives it. Each sandbox is a microVM sized from this one.
resource "azurerm_linux_virtual_machine" "vm" {
  name                  = "${local.name}-vm-01"
  location              = local.location
  resource_group_name   = local.rg
  size                  = var.vm_size
  admin_username        = var.admin_username
  network_interface_ids = [azurerm_network_interface.nic.id]
  custom_data           = base64encode(file("${path.module}/cloud-init.yaml"))
  tags                  = local.tags

  admin_ssh_key {
    username   = var.admin_username
    public_key = file(pathexpand(var.ssh_public_key_path))
  }

  os_disk {
    name                 = "${local.name}-osdisk-01"
    caching              = "ReadWrite"
    storage_account_type = "Premium_LRS"
    disk_size_gb         = var.os_disk_gb
  }

  source_image_reference {
    publisher = "Canonical"
    offer     = "ubuntu-24_04-lts"
    sku       = "server"
    version   = "latest"
  }
}
