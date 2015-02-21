﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;

namespace CodeCracker.CSharp.Usage
{
    [ExportCodeFixProvider("RemoveUnreachableCodeCodeFixProvider", LanguageNames.CSharp), Shared]
    public class RemoveUnreachableCodeCodeFixProvider : CodeFixProvider
    {
        public const string Message = "Remove unreacheable code";

        public sealed override ImmutableArray<string> GetFixableDiagnosticIds() => ImmutableArray.Create("CS0162");

        public sealed override FixAllProvider GetFixAllProvider() => RemoveUnreachableCodeFixAllProvider.Instance;

        public sealed override async Task ComputeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics.First();
            var node = root.FindNode(diagnostic.Location.SourceSpan);
            var newDoc = RemoveUnreachableCode(root, context.Document, node);
            context.RegisterFix(CodeAction.Create(Message, newDoc), diagnostic);
        }

        private static Document RemoveUnreachableCode(SyntaxNode root, Document document, SyntaxNode node) =>
            document.WithSyntaxRoot(RemoveUnreachableStatement(root, node));

        public static SyntaxNode RemoveUnreachableStatement(SyntaxNode root, SyntaxNode node)
        {
            var statement = node as StatementSyntax;//for, while, foreach, if, throw, var, etc
            if (statement != null)
            {
                if (statement.Parent.IsKind(SyntaxKind.IfStatement, SyntaxKind.WhileStatement))
                    return root.ReplaceNode(node, SyntaxFactory.Block());
                if (statement.Parent.IsKind(SyntaxKind.ElseClause))
                    return root.RemoveNode(statement.Parent, SyntaxRemoveOptions.KeepNoTrivia);
                return root.RemoveNode(statement, SyntaxRemoveOptions.KeepNoTrivia);
            }
            var localDeclaration = node.FirstAncestorOfType<LocalDeclarationStatementSyntax>();
            if (localDeclaration != null)
                return root.RemoveNode(localDeclaration, SyntaxRemoveOptions.KeepNoTrivia);
            var expression = GetExpression(node);
            if (expression.Parent.IsKind(SyntaxKind.ForStatement))
                return root.RemoveNode(expression, SyntaxRemoveOptions.KeepNoTrivia);
            var expressionStatement = expression.FirstAncestorOfType<ExpressionStatementSyntax>();
            return root.RemoveNode(expressionStatement, SyntaxRemoveOptions.KeepNoTrivia);
        }

        private static ExpressionSyntax GetExpression(SyntaxNode node)
        {
            var expression = node.Parent as ExpressionSyntax;
            var memberAccess = node.Parent as MemberAccessExpressionSyntax;
            while (memberAccess != null)
            {
                var parentMemberAccess = memberAccess.Parent as MemberAccessExpressionSyntax;
                if (parentMemberAccess != null)
                {
                    memberAccess = parentMemberAccess;
                }
                else
                {
                    expression = memberAccess.Parent as ExpressionSyntax;
                    break;
                }
            }
            return expression;
        }
    }
}