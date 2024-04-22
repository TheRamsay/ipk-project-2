using App.Enums;
using App.Models;

namespace App.Exceptions;

public class ClientUnreachableException(string message) : Exception(message);