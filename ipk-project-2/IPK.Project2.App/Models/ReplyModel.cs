using System.ComponentModel.DataAnnotations;

namespace App.Models;

public class ReplyModel : IBaseModel
{
    public required bool Status { get; set; }
    [RegularExpression("[ -~]{0,1400}", ErrorMessage = "MessageContent has to have printable characters with length from 1 to 128 characters")]
    public required string Content { get; set; }
}