output "public_ip" {
  value = azurerm_public_ip.pip.ip_address
}

output "ssh" {
  value = "ssh ${var.admin_username}@${azurerm_public_ip.pip.ip_address}"
}
