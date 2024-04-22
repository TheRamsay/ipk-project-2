using System.ComponentModel.DataAnnotations;
using App.Enums;

namespace App.Models;

public class AuthModel : IBaseModel
{
    [RegularExpression("[A-z0-9-]{1,20}", ErrorMessage = "Username has to be alphanumerical with length from 1 to 20 characters")]
    public required string Username { get; set; }

    [RegularExpression("[!-~]{1,20}", ErrorMessage = "DisplayName has to have printable characters with length from 1 to 128 characters")]
    public required string DisplayName { get; set; }

    [RegularExpression("[A-z0-9-]{1,128}", ErrorMessage = "Secret has to be alphanumerical with length from 1 to 128 characters")]
    public required string Secret { get; set; }

    public static AuthModel Parse(string data)
    {
        var parts = data.Split(' ');

        if (parts.Length != 3)
        {
            throw new ValidationException(
                "/auth command has to have 3 parts separated by space. Example: /auth username displayName secret");
        }

        var model = new AuthModel
        {
            Username = parts[0],
            Secret = parts[1],
            DisplayName = parts[2]
        };

        ModelValidator.Validate(model);

        return model;
    }
}