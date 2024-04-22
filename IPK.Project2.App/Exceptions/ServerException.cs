using App.Enums;
using App.Models;

namespace App.Exceptions;

public class ServerException(ErrorModel data) : Exception
{
    public ErrorModel ErrorData { get; private set; } = data;
}