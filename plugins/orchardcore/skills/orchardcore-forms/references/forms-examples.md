# OrchardCore.Forms Examples

## Contact Form Elements

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentType": "Form",
          "FormElementPart": {
            "Id": "contact-form"
          },
          "FormPart": {
            "Method": "POST",
            "EncType": "application/x-www-form-urlencoded",
            "EnableAntiForgeryToken": true,
            "SaveFormLocation": true
          }
        },
        {
          "ContentType": "Input",
          "FormElementPart": {
            "Id": "contact-email"
          },
          "FormInputElementPart": {
            "Name": "ContactEmail"
          },
          "InputPart": {
            "Type": "email",
            "Placeholder": "you@example.com"
          }
        },
        {
          "ContentType": "Select",
          "FormElementPart": {
            "Id": "contact-reason"
          },
          "FormInputElementPart": {
            "Name": "ContactReason"
          },
          "SelectPart": {
            "Editor": "Dropdown",
            "Options": [
              {
                "Text": "Support",
                "Value": "support"
              },
              {
                "Text": "Sales",
                "Value": "sales"
              }
            ]
          }
        }
      ]
    }
  ]
}
```

Place the element widgets in the form's `FlowPart`; the standalone content
shown above documents each serialized part mapping. Set `FormPart.Action` to
the signed URL generated for the target `HttpRequestEvent`; it is not a fixed
`/workflow/invoke/...` route.

## HTTP Workflow Validation

Use an `HttpRequestEvent` at the action route, followed by:

1. `ValidateAntiforgeryTokenTask`
2. `BindModelStateTask`
3. one or more `ValidateFormFieldTask` or `AddModelValidationErrorTask`
4. `ValidateFormTask`
5. `HttpRedirectToFormLocationTask` when returning to the saved form location

The workflow script can read a submitted value and save workflow state:

```javascript
var contactEmail = requestForm("ContactEmail");

if (contactEmail && contactEmail.includes("@")) {
    setProperty("ContactEmail", contactEmail);
    setOutcome("Valid");
} else {
    setOutcome("Invalid");
}
```
