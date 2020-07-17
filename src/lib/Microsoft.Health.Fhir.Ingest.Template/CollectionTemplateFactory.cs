﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using EnsureThat;
using Microsoft.Health.Common.Handler;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Health.Fhir.Ingest.Template
{
    public abstract class CollectionTemplateFactory<TInTemplate, TOutTemplate> : ITemplateFactory<string, TOutTemplate>
        where TInTemplate : class
        where TOutTemplate : class
    {
        private static readonly IResponsibilityHandler<TemplateContainer, TInTemplate, IList<string>> NotFoundHandler = new TemplateNotFoundHandler<TInTemplate>();

        private readonly IResponsibilityHandler<TemplateContainer, TInTemplate, IList<string>> _templateFactories;

        protected CollectionTemplateFactory(params ITemplateFactory<TemplateContainer, TInTemplate>[] factories)
        {
            EnsureArg.IsNotNull(factories, nameof(factories));
            EnsureArg.HasItems(factories, nameof(factories));

            IResponsibilityHandler<TemplateContainer, TInTemplate, IList<string>> handler = new WrappedHandlerTemplateFactory<TemplateContainer, TInTemplate>(factories[0]);
            for (int i = 1; i < factories.Length; i++)
            {
                handler = handler.Chain(new WrappedHandlerTemplateFactory<TemplateContainer, TInTemplate>(factories[i]));
            }

            // Attach NotFoundHandler at the end of the chain to throw exception if we reach end with no factory found.
            _templateFactories = handler.Chain(NotFoundHandler);
        }

        protected IResponsibilityHandler<TemplateContainer, TInTemplate, IList<string>> TemplateFactories => _templateFactories;

        protected abstract string TargetTemplateTypeName { get; }

        public TOutTemplate Create(string input, out IList<string> errors)
        {
            var rootContainer = JsonConvert.DeserializeObject<TemplateContainer>(input);
            if (!rootContainer.MatchTemplateName(TargetTemplateTypeName))
            {
                throw new InvalidTemplateException($"Expected {nameof(rootContainer.TemplateType)} value {TargetTemplateTypeName}, actual {rootContainer.TemplateType}.");
            }

            if (rootContainer.Template?.Type != JTokenType.Array)
            {
                throw new InvalidTemplateException($"Expected an array for the template property value for template type {TargetTemplateTypeName}.");
            }

            var outTemplate = BuildCollectionTemplate((JArray)rootContainer.Template, out IList<string> collectionErrors);
            errors = collectionErrors;

            return outTemplate;
        }

        public TOutTemplate Create(string input)
        {
            var outTemplate = Create(input, out IList<string> templateErrors);
            if (templateErrors?.Count > 0)
            {
                string aggregatedErrorMessage = string.Join(", \n", templateErrors);
                throw new InvalidTemplateException($"There were errors found for template type {TargetTemplateTypeName}: \n{aggregatedErrorMessage}");
            }

            return outTemplate;
        }

        protected abstract TOutTemplate BuildCollectionTemplate(JArray templateCollection, out IList<string> errors);
    }
}