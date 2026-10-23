using System.ComponentModel.DataAnnotations;

namespace final.Models;

public static class EnumDisplayExtensions
{
    public static string GetDisplayName(this Enum value) =>
        value.GetType().GetMember(value.ToString()).FirstOrDefault()?
            .GetCustomAttributes(typeof(DisplayAttribute), false)
            .OfType<DisplayAttribute>()
            .FirstOrDefault()?.GetName() ?? value.ToString();
}
