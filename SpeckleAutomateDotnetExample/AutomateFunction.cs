using System.Reflection;
using Objects;
using Objects.BuiltElements.Revit;
using Objects.Geometry;
using Speckle.Automate.Sdk;
using Speckle.Core.Api;
using Speckle.Core.Api.GraphQL.Inputs;
using Speckle.Core.Models;
using Speckle.Core.Models.Extensions;
using Speckle.Core.Models.GraphTraversal;

public static class AutomateFunction
{
    public static async Task Run(
        AutomationContext automationContext,
        FunctionInputs functionInputs
    )
    {
        Console.WriteLine("Starting execution");
        _ = typeof(ObjectsKit).Assembly; // INFO: Force objects kit to initialize

        Console.WriteLine("Receiving version");
        var commitObject = await automationContext.ReceiveVersion();
        Console.WriteLine("Received version: " + commitObject);

        var objects = DefaultTraversal
            .CreateTraversalFunc()
            .Traverse(commitObject)
            .Select(tc => ConvertToDirectShape(tc.Current, functionInputs.RevitCategory))
            .Where(ds => ds != null)
            .ToList();

        if (objects.Count == 0)
        {
            automationContext.MarkRunFailed("No valid objects found for conversion.");
            return;
        }

        var sourceModelName = (
            await automationContext.SpeckleClient.Model.Get(
                automationContext.AutomationRunData.Triggers[0].Payload.ModelId,
                automationContext.AutomationRunData.ProjectId
            )
        ).name;

        var prefix = functionInputs.TargetModelPrefix;

        if (string.IsNullOrEmpty(sourceModelName))
        {
            throw new ArgumentException(
                "Source model name cannot be null or empty",
                nameof(sourceModelName)
            );
        }

        var targetModelName = GenerateTargetModelName(sourceModelName, prefix);

        var commitCollection = new Collection()
        {
            collectionType = "direct shaped model",
            name = "Pivoted Revit model",
            elements = objects.Cast<Base>().ToList()
        };

        var newVersion = await automationContext.CreateNewVersionInProject(
            rootObject: commitCollection,
            modelName: targetModelName,
            versionMessage: $"{objects.Count} {functionInputs.RevitCategory} DirectShapes"
        );

        var targetModelId =
            (
                await automationContext.SpeckleClient.Project.GetWithModels(
                    projectId: automationContext.AutomationRunData.ProjectId,
                    modelsLimit: 1, // Efficiency: Only request what we need
                    modelsFilter: new ProjectModelsFilter(
                        search: targetModelName,
                        contributors: null,
                        sourceApps: null,
                        ids: null,
                        excludeIds: null,
                        onlyWithVersions: false
                    )
                )
            ).models?.items
            .FirstOrDefault()
            ?.id ?? string.Empty;

        if (targetModelId != string.Empty)
        {
            var modelVersionIdentifier = $"{targetModelId}@{newVersion}";
            automationContext.SetContextView([modelVersionIdentifier], false);

            Console.WriteLine($"Context view set with: {modelVersionIdentifier}");
        }

        automationContext.MarkRunSuccess(
            $"Converted OBJ to {functionInputs.RevitCategory} DirectShape"
        );
    }

    private static DirectShape? ConvertToDirectShape(Base obj, string category)
    {
        if (!Enum.TryParse<RevitCategory>(category, out var revitCategory))
        {
            throw new ArgumentException("Invalid Revit category", nameof(category));
        }

        var displayValue = obj?.TryGetDisplayValue();
        if (displayValue is null)
            return null;

        var meshes = displayValue.OfType<Mesh>().ToList();
        return meshes.Count == 0
            ? null
            : new DirectShape(
                $"A {category} from OBJ",
                revitCategory,
                baseGeometries: meshes.Cast<Base>().ToList(),
                null
            );
    }

    private static string GenerateTargetModelName(string sourceModelName, string prefix)
    {
        if (string.IsNullOrEmpty(sourceModelName))
        {
            throw new ArgumentException(
                "Source model name cannot be null or empty",
                nameof(sourceModelName)
            );
        }

        if (string.IsNullOrEmpty(prefix))
        {
            throw new ArgumentException("Prefix cannot be null or empty", nameof(prefix));
        }

        // Clean the input data
        prefix = prefix.TrimStart('/');

        if (string.IsNullOrEmpty(prefix))
        {
            throw new ArgumentException(
                "Prefix cannot be just a forward slash",
                nameof(prefix)
            );
        }

        // Process the path components
        var cleanPrefix = prefix.Trim('/');
        var parts = sourceModelName.Split('/', StringSplitOptions.RemoveEmptyEntries);

        // Construct the final path
        return $"{cleanPrefix}/{string.Join("/", parts)}";
    }
}

internal static class Processor
{
    public static Base? ProcessObject(Base baseObject)
    {
        return baseObject switch
        {
            Collection => null,
            _ => baseObject
        };
    }
}