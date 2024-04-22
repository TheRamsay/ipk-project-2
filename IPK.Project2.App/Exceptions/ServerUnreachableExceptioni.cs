using App.Enums;
using App.Models;

namespace App.Exceptions;

public class ServerUnreachableException(string message) : Exception(message);