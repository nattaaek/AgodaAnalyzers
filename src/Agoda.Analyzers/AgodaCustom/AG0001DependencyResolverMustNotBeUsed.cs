﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using StyleCop.Analyzers;
using System;
using System.Collections.Immutable;

namespace Agoda.Analyzers.AgodaCustom
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class AG0001DependencyResolverMustNotBeUsed: DiagnosticAnalyzer
	{
		public const string DiagnosticId = "AG0001";
		private static readonly LocalizableString Title = new LocalizableResourceString(nameof(CustomRulesResources.AG0001Title), CustomRulesResources.ResourceManager, typeof(CustomRulesResources));
		private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(CustomRulesResources.AG0001MessageFormat), CustomRulesResources.ResourceManager, typeof(CustomRulesResources));
		private static readonly LocalizableString Description = new LocalizableResourceString(nameof(CustomRulesResources.AG0001Description), CustomRulesResources.ResourceManager, typeof(CustomRulesResources));
		private static readonly string HelpLink = "https://github.com/DotNetAnalyzers/StyleCopAnalyzers/blob/master/documentation/SA1106.md";

		private static readonly DiagnosticDescriptor Descriptor =
			new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, AnalyzerCategory.CustomQualityRules,
				DiagnosticSeverity.Warning, AnalyzerConstants.EnabledByDefault, Description, HelpLink, WellKnownDiagnosticTags.EditAndContinue);

		private static readonly Action<SyntaxNodeAnalysisContext> DependencyResolverUsageAction = HandleDependencyResolverUsage;

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

		private static void HandleDependencyResolverUsage(SyntaxNodeAnalysisContext context)
		{
			var identifier = (context.Node as IdentifierNameSyntax);
			if (identifier?.Identifier.Text != "DependencyResolver")
				return;

            // making sure this is exactly the type of DependencyResolver we want to prevent being used
            if (context.SemanticModel.GetTypeInfo(identifier).Type.ToDisplayString() == "System.Web.Mvc.DependencyResolver")
			{
				context.ReportDiagnostic(Diagnostic.Create(Descriptor, context.Node.GetLocation()));
			}
		}


		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(DependencyResolverUsageAction, SyntaxKind.IdentifierName);
        }

    }
}
