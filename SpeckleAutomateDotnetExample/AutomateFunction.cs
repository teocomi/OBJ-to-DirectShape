using Objects;
using Objects.BuiltElements.Revit;
using Speckle.Automate.Sdk;
using Speckle.Core.Api;
using Speckle.Core.Models;
using Speckle.Core.Models.Extensions;


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
    
    var mesh = commitObject.TryGetDisplayValue();
    var ds = new DirectShape("A wall from OBJ", RevitCategory.Walls, mesh.Cast<Base>().ToList(),null);
    

    var commitId = Helpers.Send("1d4315bfc5", ds, "", "automate", 1, automationContext.SpeckleClient.Account);
    

    Console.WriteLine($"New model version published! Commit ID: {commitId}");
    
    
    // var count = commitObject
    //   .Flatten()
    //   .Count(b => b.speckle_type == functionInputs.SpeckleTypeToCount);

    // Console.WriteLine($"Counted {count} objects");

    // if (count < functionInputs.SpeckleTypeTargetCount) {
    //   automationContext.MarkRunFailed($"Counted {count} objects where {functionInputs.SpeckleTypeTargetCount} were expected");
    //   return;
    // }

    automationContext.MarkRunSuccess($"Converted OBJ to {functionInputs.RevitCategory} DirectShape");
  }
}
