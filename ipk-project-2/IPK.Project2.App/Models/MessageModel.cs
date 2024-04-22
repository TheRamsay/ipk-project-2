using System.ComponentModel.DataAnnotations;
using App.Enums;

namespace App.Models;

public class MessageModel : IBaseModel
{
    [RegularExpression("[ -~]{0,1400}", ErrorMessage = "MessageContent has to have printable characters with length from 1 to 128 characters")]
    public required string Content { get; set; }

    [RegularExpression("[!-~]{1,20}", ErrorMessage = "DisplayName has to have printable characters with length from 1 to 128 characters")]
    public string DisplayName { get; set; } = "user";

    public static MessageModel Parse(string data)
    {
        var model = new MessageModel
        {
            Content = data
        };

        ModelValidator.Validate(model);

        return model;
    }

}