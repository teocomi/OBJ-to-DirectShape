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

        // Ensure necessary types are loaded
        _ = typeof(ObjectsKit).Assembly;

        // Receive the version object
        var versionObject = await automationContext.ReceiveVersion();

        // Validate the Revit category
        var revitCategory = ValidateRevitCategory(functionInputs.RevitCategory);

        // Traverse and convert objects
        var objects = ConvertVersionObjects(versionObject, revitCategory);
        if (!objects.Any())
        {
            automationContext.MarkRunFailed("No valid objects found for conversion.");
            return;
        }

        // Get the source model name
        var sourceModelName = await GetSourceModelName(automationContext);
        ValidateSourceModelName(sourceModelName);

        // Generate target model name
        var targetModelName = GenerateTargetModelName(
            sourceModelName,
            functionInputs.TargetModelPrefix
        );

        // Create a new collection and version
        var versionCollection = new Collection
        {
            collectionType = "Directly shaped model",
            name = "Converted Revit model",
            elements = objects
        };

        var newVersion = await CreateNewVersion(
            automationContext,
            versionCollection,
            targetModelName,
            revitCategory,
            objects.Count
        );

        // Link source and target models
        await LinkSourceAndTargetModels(automationContext, targetModelName, newVersion);

        automationContext.MarkRunSuccess($"Converted OBJ to {revitCategory} DirectShape");
    }

    private static List<Base> ConvertVersionObjects(
        Base versionObject,
        string revitCategory
    )
    {
        return DefaultTraversal
            .CreateTraversalFunc()
            .Traverse(versionObject)
            .Select(tc => ConvertToDirectShape(tc.Current, revitCategory))
            .Where(ds => ds != null)
            .Cast<Base>()
            .ToList();
    }

    private static async Task<string> GetSourceModelName(AutomationContext context)
    {
        var modelInfo = await context.SpeckleClient.Model.Get(
            context.AutomationRunData.Triggers[0].Payload.ModelId,
            context.AutomationRunData.ProjectId
        );
        return modelInfo.name;
    }

    private static void ValidateSourceModelName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException(
                "Source model name cannot be null or empty",
                nameof(name)
            );
        }
    }

    private static async Task<string> CreateNewVersion(
        AutomationContext context,
        Collection versionCollection,
        string modelName,
        string revitCategory,
        int objectCount
    )
    {
        return await context.CreateNewVersionInProject(
            rootObject: versionCollection,
            modelName: modelName,
            versionMessage: $"{objectCount} {revitCategory} DirectShapes"
        );
    }

    private static async Task LinkSourceAndTargetModels(
        AutomationContext context,
        string targetModelName,
        string newVersion
    )
    {
        var targetModelId = await FindTargetModelId(context, targetModelName);

        if (!string.IsNullOrEmpty(targetModelId))
        {
            var modelVersionIdentifier = $"{targetModelId}@{newVersion}";
            context.SetContextView(new List<string>{modelVersionIdentifier}, false);
        }
    }

    private static async Task<string> FindTargetModelId(
        AutomationContext context,
        string targetModelName
    )
    {
        var project = await context.SpeckleClient.Project.GetWithModels(
            projectId: context.AutomationRunData.ProjectId,
            modelsLimit: 1,
            modelsFilter: new ProjectModelsFilter(
                search: targetModelName,
                contributors: null,
                sourceApps: null,
                ids: null,
                excludeIds: null,
                onlyWithVersions: false
            )
        );

        return project.models?.items.FirstOrDefault()?.id ?? string.Empty;
    }

    public static DirectShape? ConvertToDirectShape(Base? obj, string category)
    {
        if (!Enum.TryParse(category, out RevitCategory revitCategory))
        {
            return null;
        }

        var meshes = obj?.TryGetDisplayValue()?.OfType<Mesh>().ToList();
        if (meshes != null && meshes.Any())
        {
            return new DirectShape(
                $"A {category} from OBJ",
                revitCategory,
                meshes.Cast<Base>().ToList()
            )
            {
                // Adding the category name here makes the category human readable in the viewer
                ["categoryName"] = category,
                // Adding the meshes here also makes the DirectShape the selectable object in the viewer
                ["@displayValue"] = meshes, 
            };
        }

        return null;
    }

    public static string GenerateTargetModelName(string sourceModelName, string prefix)
    {
        if (string.IsNullOrWhiteSpace(sourceModelName))
        {
            throw new ArgumentException(
                "Source model name cannot be null, empty, or whitespace.",
                nameof(sourceModelName)
            );
        }

        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new ArgumentException(
                "Prefix cannot be null, empty, or whitespace.",
                nameof(prefix)
            );
        }

        var cleanPrefix = string.Concat(
                prefix.Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '/'
                    ? c
                    : '_')
            )
            .Trim('/');

        var safeSourceModelName = string.Concat(
                sourceModelName.Select(
                    c => char.IsLetterOrDigit(c) || c == '/' || c == '_'
                        ? c
                        : '_'
                )
            )
            .Trim();

        var parts = safeSourceModelName
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Replace(" ", "_").Trim())
            .ToArray();

        var targetModelName = $"{cleanPrefix}/{string.Join("/", parts)}";

        if (targetModelName.Length > 255)
        {
            throw new ArgumentException(
                "Generated target model name exceeds the maximum allowed length of 255 characters."
            );
        }

        return targetModelName;
    }

    public static string ValidateRevitCategory(string category)
    {
        return Enum.TryParse(typeof(RevitCategory), category, out var validCategory)
            ? validCategory.ToString()!
            : RevitCategory.GenericModel.ToString();
    }
}