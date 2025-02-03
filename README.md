# SpeckleObjToDirectShape

This function converts Speckle objects to Revit DirectShapes, compatible with Speckle Automate. It receives a model version, traverses the objects, converts them to DirectShapes, and creates a new version in the target model.

## Quick Start

1. Copy the contents of this repository into your blessed repository created from the Automate wizard.
2. Modify `AutomateFunction.cs` to include your specific logic.
3. Publish your changes (see Publishing Functions).

## Function Overview

This function targets .NET 7.0 and uses the `Speckle.Automate.Sdk` NuGet package, as well as the Objects Kit.

### Function Implementation

The main function implementation is in `AutomateFunction.cs`. Modify the `Run` method to execute your specific logic. The function receives an `AutomationContext` and a `FunctionInputs` struct.

### User Inputs

Define the user inputs required for the function in `FunctionInputs.cs`. This struct will be used to generate the JSON schema for Speckle Automate.

## Publishing Functions

Publishing your function is streamlined through a GitHub Action provided in `.github/workflows/main.yml`. Ensure you have the necessary secrets (`SPECKLE_FUNCTION_ID` and `SPECKLE_FUNCTION_TOKEN`) set up in your repository.

### Steps

1. **Restore Dependencies**: The GitHub Action restores any necessary packages.
2. **Generate JSON Schema**: The Action generates a JSON schema based on your `FunctionInputs` class.
3. **Build Docker Image**: The function is packaged into a Docker image.
4. **Register Version**: The new Docker image is registered as a new version in Speckle Automate.

After the process completes, your function will be available and discoverable in Speckle Automate.

## Changing the Project Name

If you rename your project, update the following references:

- **Dockerfile**: Update the `COPY` command to match your new project folder.
- **GitHub Actions Workflow**: Update the `working-directory` in `.github/workflows/main.yml` to point to your new project folder.