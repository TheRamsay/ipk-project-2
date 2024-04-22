using App.Enums;
using App.Models;

namespace App.Exceptions;

public class InvalidMessageReceivedException(string message) : Exception($"Invalid message received (current state: {message})");