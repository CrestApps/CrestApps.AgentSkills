---
name: orchardcore-forms
description: Skill for building and managing forms in Orchard Core with OrchardCore.Forms. Covers Form, Input, Select, TextArea, Label, Button, Validation, ValidationSummary, and ReCaptcha widgets, FormPart, FormElementPart, FormInputElementPart, conditional visibility rules with FormInputElementVisibilityPart, HTTP workflows, Fork and Join validation, Send Email with Request.Form values, anti-forgery, model state round-tripping, and custom form elements. Use this skill when requests mention Orchard Core Forms, Create and Configure Forms, Enabling the Forms Feature, Form Widget Content Types, Form Content Type Settings, Input Element Configuration, or closely related Orchard Core implementation, setup, extension, or troubleshooting work.
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
| `TextAreaPart` | `DefaultValue`, `Placeholder`, and `Rows` |
| `LabelPart` / `FormElementLabelPart` | Label widget `For`; per-element `Label` with `Option` (`None`, `Standard`, `ScreenReaders`) |
| `ButtonPart` | `Text` and `Type` (e.g., `submit`) |
| `ValidationPart` | `For`: the field name whose errors are shown |
| `ValidationSummaryPart` | Renders all model-state errors |
| `FormInputElementVisibilityPart` | `Action` (`None`, `Show`, `Hide`) and rule `Groups` |

Widget content types created by the module: `Form`, `Input`, `Select`, `TextArea`, `Label`, `Button`, `Validation`, and `ValidationSummary`. With `OrchardCore.ReCaptcha` enabled, a `ReCaptcha` widget (reCAPTCHA v2, configured under **Settings > Security > reCaptcha**) and the **Validate ReCaptcha** workflow task (`Valid`/`Invalid`/`Done`) are added.

`FormPart.EnableAntiForgeryToken` and `FormPart.SaveFormLocation` default to `true`.

The `Form` wrapper emits an anti-forgery token when enabled and, by default,
the `__RequestOriginatedFrom` workflow input used by HTTP workflow redirects.

## Enable Forms

`OrchardCore.Forms` depends on `OrchardCore.Widgets` and `OrchardCore.Flows`. Add the workflow features to process submissions without a custom controller.

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

## Conditional Visibility

Each input widget has **Visibility Settings** stored in `FormInputElementVisibilityPart`. Rules within a group are combined with AND; groups are combined with OR. Fields appear in the rule editor only after the form has been saved with them. Operators: `Is`, `IsNot`, `Empty`, `NotEmpty`, `Contains`, `DoesNotContain`, `StartsWith`, `EndsWith`, `GreaterThan`, `LessThan`.

```json
{
  "FormInputElementVisibilityPart": {
    "Action": "Show",
    "Groups": [
      {
        "Rules": [
          {
            "Field": "MembershipType",
            "Operator": "Contains",
            "Values": [ "Premium" ],
            "CaseSensitive": false
          }
        ]
      }
    ]
  }
}
```

Visibility is applied client-side; hidden fields are still accepted by the server, so validate server-side as well.

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

Typical contact-form workflow:

1. **HTTP Request** event (method `POST`, set as start activity); paste its URL path into the Form widget's **Action**.
2. **Bind Form Model State** (`BindModelStateTask`) so entered values survive a redirect back to the form.
3. **Fork** with one branch per check, each running **Validate Form Field** (or **Validate ReCaptcha**); connect every `Done` outcome to a **Join** in **Wait All** mode.
4. **Validate Form** (`ValidateFormTask`): `Invalid` goes to **Redirect** back to the form (or `HttpRedirectToFormLocationTask`), where `ValidationSummary`/`Validation` widgets show the errors; `Valid` continues.
5. **Send Email** using Liquid, e.g. subject `Contact form from {{ Request.Form.Name }}`, then **Redirect** to a thank-you page.

## Custom Form Elements

Custom elements are ordinary content parts and widgets. Add the part,
display driver, view model, and content type definition; use
`FormInputElementPart.Name` when the element must submit a value. View models
remain unsealed for model binding; seal other classes.
