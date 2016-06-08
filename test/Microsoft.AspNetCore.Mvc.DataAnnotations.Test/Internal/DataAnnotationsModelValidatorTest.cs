// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;

namespace Microsoft.AspNetCore.Mvc.DataAnnotations.Internal
{
    public class DataAnnotationsModelValidatorTest
    {
        private static IModelMetadataProvider _metadataProvider = TestModelMetadataProvider.CreateDefaultProvider();

        [Fact]
        public void Constructor_SetsAttribute()
        {
            // Arrange
            var attribute = new RequiredAttribute();

            // Act
            var validator = new DataAnnotationsModelValidator(
                new ValidationAttributeAdapterProvider(),
                attribute,
                stringLocalizer: null);

            // Assert
            Assert.Same(attribute, validator.Attribute);
        }

        public static TheoryData Validate_SetsMemberName_AsExpectedData
        {
            get
            {
                var array = new[] { new SampleModel { Name = "one" }, new SampleModel { Name = "two" } };

                // metadata, container, model, expected MemberName
                return new TheoryData<ModelMetadata, object, object, string>
                {
                    {
                        _metadataProvider.GetMetadataForProperty(typeof(string), nameof(string.Length)),
                        "Hello",
                        "Hello".Length,
                        nameof(string.Length)
                    },
                    {
                        // Validating a top-level model
                        _metadataProvider.GetMetadataForType(typeof(SampleModel)),
                        null,
                        15,
                        null
                    },
                    {
                        // Validating an element in a collection.
                        _metadataProvider.GetMetadataForType(typeof(SampleModel)),
                        array,
                        array[1],
                        null
                    },
                };
            }
        }

        [Theory]
        [MemberData(nameof(Validate_SetsMemberName_AsExpectedData))]
        public void Validate_SetsMemberName_AsExpected(
            ModelMetadata metadata,
            object container,
            object model,
            string expectedMemberName)
        {
            // Arrange
            var attribute = new Mock<TestableValidationAttribute> { CallBase = true };
            attribute
                .Setup(p => p.IsValidPublic(It.IsAny<object>(), It.IsAny<ValidationContext>()))
                .Callback((object o, ValidationContext context) =>
                {
                    Assert.Equal(expectedMemberName, context.MemberName);
                })
                .Returns(ValidationResult.Success)
                .Verifiable();
            var validator = new DataAnnotationsModelValidator(
                new ValidationAttributeAdapterProvider(),
                attribute.Object,
                stringLocalizer: null);
            var validationContext = new ModelValidationContext(
                actionContext: new ActionContext(),
                modelMetadata: metadata,
                metadataProvider: _metadataProvider,
                container: container,
                model: model);

            // Act
            var results = validator.Validate(validationContext);

            // Assert
            Assert.Empty(results);
            attribute.VerifyAll();
        }

        [Fact]
        public void Validate_Valid()
        {
            // Arrange
            var metadata = _metadataProvider.GetMetadataForType(typeof(string));
            var container = "Hello";
            var model = container.Length;

            var attribute = new Mock<ValidationAttribute> { CallBase = true };
            attribute.Setup(a => a.IsValid(model)).Returns(true);

            var validator = new DataAnnotationsModelValidator(
                new ValidationAttributeAdapterProvider(),
                attribute.Object,
                stringLocalizer: null);
            var validationContext = new ModelValidationContext(
                actionContext: new ActionContext(),
                modelMetadata: metadata,
                metadataProvider: _metadataProvider,
                container: container,
                model: model);

            // Act
            var result = validator.Validate(validationContext);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void Validate_Invalid()
        {
            // Arrange
            var metadata = _metadataProvider.GetMetadataForProperty(typeof(string), "Length");
            var container = "Hello";
            var model = container.Length;

            var attribute = new Mock<ValidationAttribute> { CallBase = true };
            attribute.Setup(a => a.IsValid(model)).Returns(false);

            var validator = new DataAnnotationsModelValidator(
                new ValidationAttributeAdapterProvider(),
                attribute.Object,
                stringLocalizer: null);
            var validationContext = new ModelValidationContext(
                actionContext: new ActionContext(),
                modelMetadata: metadata,
                metadataProvider: _metadataProvider,
                container: container,
                model: model);

            // Act
            var result = validator.Validate(validationContext);

            // Assert
            var validationResult = result.Single();
            Assert.Empty(validationResult.MemberName);
            Assert.Equal(attribute.Object.FormatErrorMessage("Length"), validationResult.Message);
        }

        [Fact]
        public void Validate_ValidationResultSuccess()
        {
            // Arrange
            var metadata = _metadataProvider.GetMetadataForType(typeof(string));
            var container = "Hello";
            var model = container.Length;

            var attribute = new Mock<TestableValidationAttribute> { CallBase = true };
            attribute
                .Setup(p => p.IsValidPublic(It.IsAny<object>(), It.IsAny<ValidationContext>()))
                .Returns(ValidationResult.Success);
            var validator = new DataAnnotationsModelValidator(
                new ValidationAttributeAdapterProvider(),
                attribute.Object,
                stringLocalizer: null);
            var validationContext = new ModelValidationContext(
                actionContext: new ActionContext(),
                modelMetadata: metadata,
                metadataProvider: _metadataProvider,
                container: container,
                model: model);

            // Act
            var result = validator.Validate(validationContext);

            // Assert
            Assert.Empty(result);
        }

        public static TheoryData<string, IEnumerable<string>, IEnumerable<ModelValidationResult>>
            Valdate_ReturnsExpectedResults_Data
        {
            get
            {
                var errorMessage = "Some error message";
                return new TheoryData<string, IEnumerable<string>, IEnumerable<ModelValidationResult>>
                {
                    {
                        errorMessage,
                        null,
                        new[] { new ModelValidationResult(memberName: string.Empty, message: errorMessage) } },
                    {
                        errorMessage,
                        Enumerable.Empty<string>(),
                        new[] { new ModelValidationResult(memberName: string.Empty, message: errorMessage) }
                    },
                    {
                        errorMessage,
                        new[] { (string)null },
                        new[] { new ModelValidationResult(memberName: string.Empty, message: errorMessage) }
                    },
                    {
                        errorMessage,
                        new[] { string.Empty },
                        new[] { new ModelValidationResult(memberName: string.Empty, message: errorMessage) }
                    },
                    {
                        errorMessage,
                        // Name matches ValidationContext.MemberName.
                        new[] { nameof(string.Length) },
                        new[] { new ModelValidationResult(memberName: string.Empty, message: errorMessage) }
                    },
                    {
                        errorMessage,
                        new[] { "AnotherName" },
                        new[] { new ModelValidationResult(memberName: "AnotherName", message: errorMessage) }
                    },
                    {
                        errorMessage,
                        new[] { "[1]" },
                        new[] { new ModelValidationResult(memberName: "[1]", message: errorMessage) }
                    },
                    {
                        errorMessage,
                        new[] { "Name1", "Name2" },
                        new[]
                        {
                            new ModelValidationResult(memberName: "Name1", message: errorMessage),
                            new ModelValidationResult(memberName: "Name2", message: errorMessage),
                        }
                    },
                    {
                        errorMessage,
                        new[] { "[0]", "[2]" },
                        new[]
                        {
                            new ModelValidationResult(memberName: "[0]", message: errorMessage),
                            new ModelValidationResult(memberName: "[2]", message: errorMessage),
                        }
                    },
                };
            }
        }

        [Theory]
        [MemberData(nameof(Valdate_ReturnsExpectedResults_Data))]
        public void Valdate_ReturnsExpectedResults(
            string errorMessage,
            IEnumerable<string> memberNames,
            IEnumerable<ModelValidationResult> expectedResults)
        {
            // Arrange
            var metadata = _metadataProvider.GetMetadataForProperty(typeof(string), nameof(string.Length));
            var container = "Hello";
            var model = container.Length;

            var attribute = new Mock<TestableValidationAttribute> { CallBase = true };
            attribute
                 .Setup(p => p.IsValidPublic(It.IsAny<object>(), It.IsAny<ValidationContext>()))
                 .Returns(new ValidationResult(errorMessage, memberNames));

            var validator = new DataAnnotationsModelValidator(
                new ValidationAttributeAdapterProvider(),
                attribute.Object,
                stringLocalizer: null);
            var validationContext = new ModelValidationContext(
                actionContext: new ActionContext(),
                modelMetadata: metadata,
                metadataProvider: _metadataProvider,
                container: container,
                model: model);

            // Act
            var results = validator.Validate(validationContext);

            // Assert
            Assert.Equal(expectedResults, results, ModelValidationResultComparer.Instance);
        }

        [Fact]
        public void Validate_IsValidFalse_StringLocalizerReturnsLocalizerErrorMessage()
        {
            // Arrange
            var metadata = _metadataProvider.GetMetadataForType(typeof(string));
            var container = "Hello";

            var attribute = new MaxLengthAttribute(4);
            attribute.ErrorMessage = "{0} should have no more than {1} characters.";

            var localizedString = new LocalizedString(attribute.ErrorMessage, "Longueur est invalide : 4");
            var stringLocalizer = new Mock<IStringLocalizer>();
            stringLocalizer.Setup(s => s[attribute.ErrorMessage, It.IsAny<object[]>()]).Returns(localizedString);

            var validator = new DataAnnotationsModelValidator(
                new ValidationAttributeAdapterProvider(),
                attribute,
                stringLocalizer.Object);
            var validationContext = new ModelValidationContext(
                actionContext: new ActionContext(),
                modelMetadata: metadata,
                metadataProvider: _metadataProvider,
                container: container,
                model: "abcde");

            // Act
            var result = validator.Validate(validationContext);

            // Assert
            var validationResult = result.Single();
            Assert.Empty(validationResult.MemberName);
            Assert.Equal("Longueur est invalide : 4", validationResult.Message);
        }

        [Fact]
        public void Validate_CanUseRequestServices_WithinValidationAttribute()
        {
            // Arrange
            var service = new Mock<IExampleService>();
            service.Setup(x => x.DoSomething()).Verifiable();

            var provider = new ServiceCollection().AddSingleton(service.Object).BuildServiceProvider();

            var httpContext = new Mock<HttpContext>();
            httpContext.SetupGet(x => x.RequestServices).Returns(provider);

            var attribute = new Mock<TestableValidationAttribute> { CallBase = true };
            attribute
                .Setup(p => p.IsValidPublic(It.IsAny<object>(), It.IsAny<ValidationContext>()))
                .Callback((object o, ValidationContext context) =>
                {
                    var receivedService = context.GetService<IExampleService>();
                    Assert.Equal(service.Object, receivedService);
                    receivedService.DoSomething();
                });

            var validator = new DataAnnotationsModelValidator(
                new ValidationAttributeAdapterProvider(),
                attribute.Object,
                stringLocalizer: null);

            var validationContext = new ModelValidationContext(
                actionContext: new ActionContext
                {
                    HttpContext = httpContext.Object
                },
                modelMetadata: _metadataProvider.GetMetadataForType(typeof(object)),
                metadataProvider: _metadataProvider,
                container: null,
                model: new object());

            // Act
            var results = validator.Validate(validationContext);

            // Assert
            service.Verify();
        }

        private const string LocalizationKey = "LocalizeIt";

        public static TheoryData Validate_AttributesIncludeValues
        {
            get
            {
                var pattern = "apattern";
                var length = 5;
                var regex = "^((?!" + pattern + ").)*$";

                return new TheoryData<ValidationAttribute, string, object[]>
                {
                    {
                        new RegularExpressionAttribute(regex) { ErrorMessage = LocalizationKey },
                        pattern,
                        new object[] { nameof(SampleModel), regex }
                    },
                    {
                        new MaxLengthAttribute(length) { ErrorMessage = LocalizationKey },
                        pattern,
                        new object[] { nameof(SampleModel), length }},
                    {
                        new MaxLengthAttribute(length) { ErrorMessage = LocalizationKey },
                        pattern,
                        new object[] { nameof(SampleModel), length }
                    },
                    {
                        new CompareAttribute(pattern) { ErrorMessage = LocalizationKey },
                        pattern,
                        new object[] { nameof(SampleModel), pattern }},
                    {
                        new MinLengthAttribute(length) { ErrorMessage = LocalizationKey },
                        "a",
                        new object[] { nameof(SampleModel), length }
                    },
                    {
                        new CreditCardAttribute() { ErrorMessage = LocalizationKey },
                        pattern,
                        new object[] { nameof(SampleModel), "CreditCard" }
                    },
                    {
                        new StringLengthAttribute(length) { ErrorMessage = LocalizationKey, MinimumLength = 1},
                        string.Empty,
                        new object[] { nameof(SampleModel), 1, length }
                    },
                    {
                        new RangeAttribute(0, length) { ErrorMessage = LocalizationKey },
                        pattern,
                        new object[] { nameof(SampleModel), 0, length}
                    },
                    {
                        new EmailAddressAttribute() { ErrorMessage = LocalizationKey },
                        pattern,
                        new object[] { nameof(SampleModel), "EmailAddress" }
                    },
                    {
                        new PhoneAttribute() { ErrorMessage = LocalizationKey },
                        pattern,
                        new object[] { nameof(SampleModel), "PhoneNumber" }
                    },
                    {
                        new UrlAttribute() { ErrorMessage = LocalizationKey },
                        pattern,
                        new object[] { nameof(SampleModel), "Url"  }
                    }
                };
            }
        }

        [Theory]
        [MemberData(nameof(Validate_AttributesIncludeValues))]
        public void Validate_IsValidFalse_StringLocalizerGetsArguments(
            ValidationAttribute attribute,
            string model,
            object[] values)
        {
            // Arrange
            var stringLocalizer = new Mock<IStringLocalizer>();

            var validator = new DataAnnotationsModelValidator(
                new ValidationAttributeAdapterProvider(),
                attribute,
                stringLocalizer.Object);

            var metadata = _metadataProvider.GetMetadataForType(typeof(SampleModel));
            var validationContext = new ModelValidationContext(
                actionContext: new ActionContext(),
                modelMetadata: metadata,
                metadataProvider: _metadataProvider,
                container: null,
                model: model);

            // Act
            validator.Validate(validationContext);

            // Assert
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(values) + " " + attribute.GetType().Name;

            stringLocalizer.Verify(l => l[LocalizationKey, values], json);
        }

        public abstract class TestableValidationAttribute : ValidationAttribute
        {
            protected override ValidationResult IsValid(object value, ValidationContext validationContext)
            {
                return IsValidPublic(value, validationContext);
            }

            public abstract ValidationResult IsValidPublic(object value, ValidationContext validationContext);
        }

        private class SampleModel
        {
            public string Name { get; set; }
        }

        public interface IExampleService
        {
            void DoSomething();
        }

        private class ModelValidationResultComparer : IEqualityComparer<ModelValidationResult>
        {
            public static readonly ModelValidationResultComparer Instance = new ModelValidationResultComparer();

            private ModelValidationResultComparer()
            {
            }

            public bool Equals(ModelValidationResult x, ModelValidationResult y)
            {
                if (x == null || y == null)
                {
                    return x == null && y == null;
                }

                return string.Equals(x.MemberName, y.MemberName, StringComparison.Ordinal) &&
                    string.Equals(x.Message, y.Message, StringComparison.Ordinal);
            }

            public int GetHashCode(ModelValidationResult obj)
            {
                if (obj == null)
                {
                    throw new ArgumentNullException(nameof(obj));
                }

                var hashCodeCombiner = HashCodeCombiner.Start();
                hashCodeCombiner.Add(obj.MemberName, StringComparer.Ordinal);
                hashCodeCombiner.Add(obj.Message, StringComparer.Ordinal);

                return hashCodeCombiner.CombinedHash;
            }
        }
    }
}
