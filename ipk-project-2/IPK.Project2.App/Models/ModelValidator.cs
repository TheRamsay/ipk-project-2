using System.ComponentModel.DataAnnotations;

namespace App.Models;

public class ModelValidator
{
    public static void Validate(IBaseModel model)
    {
        var validationContext = new ValidationContext(model);

        Validator.ValidateObject(model, validationContext, true);
    }
}