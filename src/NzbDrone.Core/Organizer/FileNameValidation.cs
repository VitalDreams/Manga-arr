using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentValidation;
using FluentValidation.Validators;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.Organizer
{
    public static class FileNameValidation
    {
        internal static readonly Regex OriginalTokenRegex = new Regex(@"(\{original[- ._](?:title|filename)\})",
                                                                            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static IRuleBuilderOptions<T, string> ValidBookFormat<T>(this IRuleBuilder<T, string> ruleBuilder)
        {
            ruleBuilder.SetValidator(new NotEmptyValidator(null));
            ruleBuilder.SetValidator(new IllegalCharactersValidator());

            return ruleBuilder.SetValidator(new ValidStandardTrackFormatValidator());
        }

        public static IRuleBuilderOptions<T, string> ValidAuthorFolderFormat<T>(this IRuleBuilder<T, string> ruleBuilder)
        {
            ruleBuilder.SetValidator(new NotEmptyValidator(null));
            ruleBuilder.SetValidator(new IllegalCharactersValidator());

            return ruleBuilder.SetValidator(new AuthorFolderFormatValidator()).WithMessage("Must contain Author name");
        }
    }

    public class ValidStandardTrackFormatValidator : PropertyValidator
    {
        private static readonly Regex Mylar3TitleTokenRegex = new Regex(@"\$Series|\{Title\}",
                                                                            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex Mylar3PartTokenRegex = new Regex(@"\$IssueN|\{PartNumber\}|\{Part\}",
                                                                          RegexOptions.Compiled | RegexOptions.IgnoreCase);

        protected override string GetDefaultMessageTemplate() => "Must contain Book Title AND PartNumber, OR Original Title";

        protected override bool IsValid(PropertyValidatorContext context)
        {
            if (context.PropertyValue is not string value)
            {
                return false;
            }

            return (FileNameBuilder.BookTitleRegex.IsMatch(value) && FileNameBuilder.PartRegex.IsMatch(value)) ||
                   (Mylar3TitleTokenRegex.IsMatch(value) && Mylar3PartTokenRegex.IsMatch(value)) ||
                   FileNameValidation.OriginalTokenRegex.IsMatch(value);
        }
    }

    public class AuthorFolderFormatValidator : PropertyValidator
    {
        private static readonly Regex Mylar3AuthorTokenRegex = new Regex(@"\$Series|\{Author\}",
                                                                            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        protected override string GetDefaultMessageTemplate() => "Must contain Author name";

        protected override bool IsValid(PropertyValidatorContext context)
        {
            if (context.PropertyValue is not string value)
            {
                return false;
            }

            return FileNameBuilder.AuthorNameRegex.IsMatch(value) || Mylar3AuthorTokenRegex.IsMatch(value);
        }
    }

    public class IllegalCharactersValidator : PropertyValidator
    {
        private readonly char[] _invalidPathChars = Path.GetInvalidPathChars();

        protected override string GetDefaultMessageTemplate() => "Contains illegal characters: {InvalidCharacters}";

        protected override bool IsValid(PropertyValidatorContext context)
        {
            var value = context.PropertyValue as string;

            if (value.IsNullOrWhiteSpace())
            {
                return true;
            }

            var invalidCharacters = _invalidPathChars.Where(i => value!.IndexOf(i) >= 0).ToList();
            if (invalidCharacters.Any())
            {
                context.MessageFormatter.AppendArgument("InvalidCharacters", string.Join("", invalidCharacters));
                return false;
            }

            return true;
        }
    }
}
