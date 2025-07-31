
# Workflow: Development Team

This workflow orchestrates a team of AI agents to take a feature from an idea to a fully implemented and reviewed piece of code.

## Agents

- **feature-requirements-analyst:** Gathers detailed requirements from the user.
- **implementation-planner:** Creates a technical plan based on the requirements.
- **senior-developer:** Implements the code according to the plan, following TDD.
- **code-reviewer:** Reviews the implemented code for quality and correctness.

## Workflow Steps

1.  **Start: Feature Idea**
    -   **Input:** A high-level description of a feature from the user. $ARGUMENTS
    -   **Agent:** `feature-requirements-analyst`
    -   **Action:** The agent interacts with the user to flesh out the details.
    -   **Output:** A detailed Feature Requirements Document (FRD).
    -   **Wait:** MUST Wait for FRD approval by user before continuing workflow.

2.  **Step 2: Technical Planning**
    -   **Input:** The FRD from the previous step.
    -   **Agent:** `implementation-planner`
    -   **Action:** The agent analyzes the codebase and the FRD to create a detailed technical plan.
    -   **Output:** A Proposed Implementation Plan (PRP) with effort and success scores.
    -   **Wait:** MUST Wait for PRP approval by user before continuing workflow.

3.  **Step 3: Implementation**
    -   **Input:** The PRP from the previous step.
    -   **Agent:** `senior-developer`
    -   **Action:** The agent implements the feature following TDD and the steps outlined in the PRP.
    -   **Output:** The modified source code and passing test results.    

4.  **Step 4: Code Review (Loop)**
    -   **Input:** The implemented code and the PRP.
    -   **Agent:** `code-reviewer`
    -   **Action:** The agent reviews the code against the plan and quality standards.
    -   **Output:** A Code Review (CR) document with a verdict.

5.  **Conditional Step: Rework**
    -   **Condition:** If the Code Review verdict is "CHANGES REQUESTED".
    -   **Input:** The CR document.
    -   **Agent:** `senior-developer`
    -   **Action:** The developer addresses all feedback from the reviewer.
    -   **Output:** Updated source code.
    -   **Next Step:** Go back to **Step 4: Code Review**.

6.  **End: Feature Complete**
    -   **Condition:** If the Code Review verdict is "APPROVED".
    -   **Action:** The workflow concludes.
    -   **Output:** A final summary of the process.

## Final Summary Generation

Upon successful completion, generate a `Workflow/workflow-name-summary.md` file containing:
- **Feature:** The name of the implemented feature.
- **What was accomplished:** A brief, high-level summary of the changes.
- **Process Feedback & Lessons Learned:**
  - How well did the agents collaborate?
  - Were there any misunderstandings between agents?
  - How could the prompts for each agent be improved for the next run?
  - Was the final result aligned with the initial request?
