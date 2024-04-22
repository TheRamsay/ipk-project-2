using System.ComponentModel.DataAnnotations;
using App.Enums;

namespace App.Models;

public class RenameModel : IBaseModel
{
    [RegularExpression("[!-~]{1,20}", ErrorMessage = "DisplayName has to have printable characters with length from 1 to 128 characters")]
    public required string DisplayName { get; set; }

    public static RenameModel Parse(string data)
    {
        var parts = data.Split(' ');

        if (parts.Length != 1)
        {
            throw new ValidationException(
                "/rename command has to have 1 part separated by space. Example: /rename displayName");
        }

        var model = new RenameModel
        {
            DisplayName = parts[0]
        };

        ModelValidator.Validate(model);

        return model;
    }
}