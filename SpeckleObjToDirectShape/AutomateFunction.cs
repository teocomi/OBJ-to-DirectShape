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
        InitialiseObjectsKit();

        // Receive the version object
        var versionObject = await ReceiveVersion(automationContext);

        // Validate the Revit category
        var revitCategory = ValidateRevitCategory(functionInputs.RevitCategory);

        // Traverse and convert objects
        var objects = ConvertVersionObjects(versionObject, revitCategory);
        if (!objects.Any())
        {
            FailExecution(automationContext, "No valid objects found for conversion.");
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
        var versionCollection = CreateVersionCollection(objects);
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

    private static void InitialiseObjectsKit()
    {
        _ = typeof(ObjectsKit).Assembly;
        Console.WriteLine("Objects kit initialised");
    }

    private static async Task<Base> ReceiveVersion(AutomationContext automationContext)
    {
        Console.WriteLine("Receiving version");
        var versionObject = await automationContext.ReceiveVersion();
        Console.WriteLine($"[INFO] Received version: {versionObject}");
        return versionObject;
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

    private static void FailExecution(AutomationContext context, string message)
    {
        context.MarkRunFailed(message);
        Console.WriteLine($"[ERROR] {message}");
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

    private static Collection CreateVersionCollection(List<Base> objects)
    {
        return new Collection
        {
            collectionType = "Directly shaped model",
            name = "Converted Revit model",
            elements = objects
        };
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
            context.SetContextView([modelVersionIdentifier], false);
            Console.WriteLine($"Context view set with: {modelVersionIdentifier}");
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
            Console.WriteLine(
                $"[WARN] Invalid Revit category '{category}' provided. Skipping object conversion."
            );
            return null;
        }

        var meshes = obj?.TryGetDisplayValue()?.OfType<Mesh>().ToList();
        if (meshes == null || !meshes.Any())
        {
            Console.WriteLine(
                $"[INFO] No display meshes found for object '{obj?.id}'. Skipping conversion."
            );
            return null;
        }

        return new DirectShape(
            $"A {category} from OBJ",
            revitCategory,
            meshes.Cast<Base>().ToList()
        );
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
            .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
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
        if (Enum.TryParse(typeof(RevitCategory), category, out var validCategory))
        {
            return validCategory.ToString()!;
        }

        Console.WriteLine(
            $"[WARN] Invalid Revit category '{category}' provided. Defaulting to 'Generic Model'."
        );
        return RevitCategory.GenericModel.ToString();
    }
}