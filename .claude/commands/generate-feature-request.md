# Generate Feature Request

## Feature Description: $ARGUMENTS

Use the feature-requirements-analyst agent to generate a comprehensive Feature Request (FR) for a new feature in the .NET 9 Worker Service. This command will use the base template to ensure consistency and completeness.

**CRITICAL: The goal is to define the "what" and "why" of a feature, not the "how". The output should be a clear specification for a developer to understand.**

## Process

1.  **Understand the Feature**
    - Take the `$ARGUMENTS` provided as the core idea for the feature.
    - If the description is ambiguous, ask for clarification before proceeding.

2.  **Use the Base Template**
    - Read `FRs/templates/fr_worker_base.md` as the foundation for the new FR.
    - The structure of the generated file must follow this template.

3.  **Elaborate and Fill Sections**
    - Using the feature `$ARGUMENTS`, thoughtfully fill in each section of the template.
    - **FEATURE NAME:** Create a concise and descriptive name for the feature.
    - **FEATURE PURPOSE:** Clearly articulate the problem this feature solves and the value it provides.
    - **CORE FUNCTIONALITY:** List the primary user-facing or system-level capabilities. Be specific.
    - **CLEAN ARCHITECTURE LAYERS:** Analyze and list which projects/layers of the solution will be impacted by the change.
    - **CONFIGURATION REQUIREMENTS:** Specify any changes to `appsettings.json` or new environment variables.
    - **PACKAGE MANAGEMENT:** List any new NuGet packages that will be needed.
    - **TESTING REQUIREMENTS:** Define what is needed to verify the feature works correctly (e.g., manual checks, new integration tests).
    - **SUCCESS CRITERIA:** Create a checklist of objective, verifiable conditions for the feature to be considered complete.
    - **EXPECTED COMPLEXITY LEVEL:** Estimate the complexity and provide a brief justification.

## Output

-   **Filename Generation**: Create a concise, maximum 3 words, in a kebab-case filename from the feature name (e.g., `new-feature-name.md`).
-   **Save Location**: Save the generated file as `FRs/{generated-filename}.md`.

## Quality Checklist

- [ ] Does the FR clearly state its purpose?
- [ ] Is the core functionality broken down into a clear list?
- [ ] Are all relevant sections of the template filled out?
- [ ] Are the success criteria specific and measurable?
- [ ] Is the output file correctly named and placed?