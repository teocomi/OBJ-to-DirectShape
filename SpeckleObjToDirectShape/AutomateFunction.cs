using Objects;
using Objects.BuiltElements.Revit;
using Objects.Geometry;
using Speckle.Automate.Sdk;
using Speckle.Core.Api.GraphQL.Inputs;
using Speckle.Core.Models;
using Speckle.Core.Models.Extensions;
using Speckle.Core.Models.GraphTraversal;

namespace SpeckleObjToDirectShape;

public static class AutomateFunction
{
    public static async Task Run(
        AutomationContext automationContext,
        FunctionInputs functionInputs
    )
    {
        Console.WriteLine("Starting execution");

        // Force objects kit to initialize to ensure all necessary types are loaded
        _ = typeof(ObjectsKit).Assembly; // INFO: Force objects kit to initialize

        Console.WriteLine("Receiving version");

        // Receive the version of the model to be processed
        var commitObject = await automationContext.ReceiveVersion();
        Console.WriteLine("Received version: " + commitObject);

        // Traverse the commit object to find and convert relevant objects
        var objects = DefaultTraversal
            .CreateTraversalFunc()
            .Traverse(commitObject)
            .Select(tc => ConvertToDirectShape(tc.Current, functionInputs.RevitCategory))
            .Where(ds => ds != null)
            .ToList();

        if (!objects.Any())
        {
            automationContext.MarkRunFailed("No valid objects found for conversion.");
            return;
        }

        // Get the source model name from the Speckle server
        var sourceModelName = (
            await automationContext.SpeckleClient.Model.Get(
                automationContext.AutomationRunData.Triggers[0].Payload.ModelId,
                automationContext.AutomationRunData.ProjectId
            )
        ).name;

        if (string.IsNullOrEmpty(sourceModelName))
        {
            throw new ArgumentException(
                "Source model name cannot be null or empty",
                nameof(sourceModelName)
            );
        }

        // Generate a new target model name based on the source model name and prefix
        var targetModelName = GenerateTargetModelName(
            sourceModelName,
            functionInputs.TargetModelPrefix
        );

        // Create a new collection of converted objects
        var commitCollection = new Collection
        {
            collectionType = "direct shaped model",
            name = "Pivoted Revit model",
            elements = objects.Cast<Base>().ToList()
        };

        // Create a new version in the project with the converted objects
        var newVersion = await automationContext.CreateNewVersionInProject(
            rootObject: commitCollection,
            modelName: targetModelName,
            versionMessage: $"{objects.Count} {functionInputs.RevitCategory} DirectShapes"
        );

        // Using ProjectModelsFilter to search for the target model by name - sadly all inputs are mandatory.
        var targetModelId = (
            await automationContext.SpeckleClient.Project.GetWithModels(
                projectId: automationContext.AutomationRunData.ProjectId,
                modelsLimit: 1,
                modelsFilter: new ProjectModelsFilter(
                    search: targetModelName,
                    contributors: null,
                    sourceApps: null,
                    ids: null,
                    excludeIds: null,
                    onlyWithVersions: false
                )
            )
        ).models?.items.FirstOrDefault()?.id ?? string.Empty;

        // all of that is only necessary to link the automate results of the source model to link across to the converted model
        if (!string.IsNullOrEmpty(targetModelId))
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
        if (!Enum.TryParse(category, out RevitCategory revitCategory))
        {
            throw new ArgumentException("Invalid Revit category", nameof(category));
        }

        var meshes = obj?.TryGetDisplayValue()?.OfType<Mesh>().ToList();
        return meshes?.Any() == true
            ? new DirectShape(
                $"A {category} from OBJ",
                revitCategory,
                meshes.Cast<Base>().ToList(),
                null
            )
            : null;
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

        var cleanPrefix = prefix.Trim('/');
        var parts = sourceModelName.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return $"{cleanPrefix}/{string.Join("/", parts)}";
    }
}