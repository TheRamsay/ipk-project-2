using System.ComponentModel.DataAnnotations;
using App.Enums;

namespace App.Models;

public class JoinModel : IBaseModel
{
    [RegularExpression("[A-z0-9-]{1,20}", ErrorMessage = "ChannelId has to be alphanumerical with length from 1 to 20 characters")]
    public required string ChannelId { get; set; }

    [RegularExpression("[!-~]{1,20}", ErrorMessage = "DisplayName has to have printable characters with length from 1 to 128 characters")]
    public string DisplayName { get; set; } = string.Empty;

    public static JoinModel Parse(string data)
    {
        var parts = data.Split(' ');

        if (parts.Length != 1)
        {
            throw new ValidationException(
                "/join command has to have 1 parts separated by space. Example: /join channelId");
        }

        var model = new JoinModel
        {
            ChannelId = parts[0]
        };

        ModelValidator.Validate(model);

        return model;
    }
}