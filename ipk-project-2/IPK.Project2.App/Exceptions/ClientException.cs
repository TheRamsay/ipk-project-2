using App.Enums;
using App.Models;

namespace App.Exceptions;

public class ClientException(ErrorModel data) : Exception
{
    public ErrorModel ErrorData { get; private set; } = data;
}