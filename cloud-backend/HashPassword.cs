using System;

// Simple BCrypt implementation for password hashing
var password = "Admin@12345678!";

// Using BCrypt.Net-Next (same as the backend)
// Work factor: 12 (same as backend in AuthController.cs)
var hash = BCrypt.Net.BCrypt.HashPassword(password, 12);

Console.WriteLine("Password: " + password);
Console.WriteLine("Hash: " + hash);
Console.WriteLine();
Console.WriteLine("SQL Update Command:");
Console.WriteLine($"UPDATE users SET \"PasswordHash\" = '{hash}' WHERE \"Email\" = 'admin@safesignal.com';");
