using Amazon.Runtime.Internal.Util;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace FileTransferazor.Server.Models
{
    public class FormDataJsonBinder : IModelBinder
    {
        private readonly ILogger<FormDataJsonBinder> _logger;

        public FormDataJsonBinder(ILogger<FormDataJsonBinder> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            if (bindingContext == null)
            {
                throw new ArgumentNullException(nameof(bindingContext));
            }

            var modelName = bindingContext.ModelName;
            var valueProviderResult = bindingContext.ValueProvider.GetValue(modelName);

            if (valueProviderResult == ValueProviderResult.None)
            {
                return Task.CompletedTask;
            }

            bindingContext.ModelState.SetModelValue(modelName, valueProviderResult);

            var value = valueProviderResult.FirstValue;

            if (string.IsNullOrEmpty(value))
            {
                return Task.CompletedTask;
            }

            try
            {
                var result = JsonSerializer.Deserialize(value, bindingContext.ModelType);
                bindingContext.Result = ModelBindingResult.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                bindingContext.Result = ModelBindingResult.Failed();
            }

            return Task.CompletedTask;
        }
    }
}
