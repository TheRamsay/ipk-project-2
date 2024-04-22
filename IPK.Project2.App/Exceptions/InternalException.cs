using App.Enums;
using App.Models;

namespace App.Exceptions;

public class InternalException(string message) : Exception(message);