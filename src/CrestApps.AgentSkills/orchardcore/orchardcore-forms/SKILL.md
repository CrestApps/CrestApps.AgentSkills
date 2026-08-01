---
name: orchardcore-forms
description: Skill for building and managing forms in Orchard Core with OrchardCore.Forms. Covers Form, Input, Select, TextArea, Button, validation widgets, FormPart, FormElementPart, FormInputElementPart, HTTP workflows, anti-forgery, and custom form elements. Use this skill when requests mention Orchard Core Forms, Create and Configure Forms, Enabling the Forms Feature, Form Widget Content Types, Form Content Type Settings, Input Element Configuration, or closely related Orchard Core implementation, setup, extension, or troubleshooting work.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Forms

Enable `OrchardCore.Forms` and use its widget types inside a `FlowPart`.
`Form` contains `FormElementPart`, `FormPart`, and `FlowPart`. `Input`,
`Select`, and `TextArea` each contain `FormInputElementPart`,
`FormElementPart`, `FormElementLabelPart`, their specific element part,
`FormElementValidationPart`, and `FormInputElementVisibilityPart`.

| Part | Current responsibility |
|---|---|
| `FormPart` | `Action`, `Method`, `WorkflowTypeId`, `EncType`, anti-forgery, and form-location behavior |
| `FormElementPart` | Element `Id` |
| `FormInputElementPart` | Submitted field `Name` |
| `InputPart` | Input `Type`, `DefaultValue`, and `Placeholder` |
| `SelectPart` | `Options`, `DefaultValue`, and `Editor` |

The `Form` wrapper emits an anti-forgery token when enabled and, by default,
the `__RequestOriginatedFrom` workflow input used by HTTP workflow redirects.

## Enable Forms

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Forms",
        "OrchardCore.Flows",
        "OrchardCore.Widgets",
        "OrchardCore.Workflows",
        "OrchardCore.Workflows.Http"
      ]
    }
  ]
}
```

## Form Element Content

`SelectPart.Options` is an array of `SelectOption` objects, not newline text:

```json
{
  "ContentType": "Select",
  "FormInputElementPart": {
    "Name": "ContactReason"
  },
  "FormElementPart": {
    "Id": "contact-reason"
  },
  "SelectPart": {
    "Editor": "Dropdown",
    "DefaultValue": "support",
    "Options": [
      {
        "Text": "Technical support",
        "Value": "support"
      },
      {
        "Text": "Sales",
        "Value": "sales"
      }
    ]
  }
}
```

## Workflow Submission and Validation

Set the form action to a workflow HTTP endpoint and start the workflow with
the real `HttpRequestEvent`; `FormSubmissionEvent` is not an Orchard Core
activity. `FormPart.WorkflowTypeId` exists on the model, but the built-in
editor does not render an input for it; set it through code or imported data
when needed. It does not replace an HTTP event route.

Forms supplies these validation activities when `OrchardCore.Workflows` is
enabled:

- `ValidateAntiforgeryTokenTask`
- `BindModelStateTask`
- `ValidateFormFieldTask`
- `AddModelValidationErrorTask`
- `ValidateFormTask`
- `HttpRedirectToFormLocationTask`

In JavaScript workflow expressions, use the HTTP globals `requestForm(name)`
or `deserializeRequestData()` and workflow globals `property(name)`,
`setProperty(name, value)`, and `setOutcome(name)`. Do not use fictional
`requestFormAsDict`, `addModelError`, or `modelState` globals.

```javascript
var email = requestForm("ContactEmail");

if (!email || !email.includes("@")) {
    setOutcome("Invalid");
} else {
    setProperty("ContactEmail", email);
    setOutcome("Valid");
}
```

For required fields and model-state errors, add `ValidateFormFieldTask` or
`AddModelValidationErrorTask` in the workflow and then use
`ValidateFormTask` to branch to its `Valid` or `Invalid` outcome.

## Custom Form Elements

Custom elements are ordinary content parts and widgets. Add the part,
display driver, view model, and content type definition; use
`FormInputElementPart.Name` when the element must submit a value. View models
remain unsealed for model binding; seal other classes.
