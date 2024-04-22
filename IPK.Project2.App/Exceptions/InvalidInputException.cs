using App.Enums;
using App.Models;

namespace App.Exceptions;

public class InvalidInputException(ProtocolState currentState) : Exception($"Invalid input (current state: {currentState})");