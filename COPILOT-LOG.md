# Copilot log (week 5)

Tool: GitHub Copilot Chat in Visual Studio, on the ApexPost solution, on 2026-11-15. Every generated line was read before it was kept, and every kept test was run.

## Task A: Copilot writes unit tests for one service (`ParcelService`)

### Round 1: `UploadScansAsync`

| Item | Entry |
|---|---|
| Prompt | `@workspace /tests Write xUnit tests with Moq for ParcelService.UploadScansAsync in ApexPost.Application/Services/ParcelService.cs. Use Arrange/Act/Assert, name tests Method_Condition_ExpectedResult, use a fixed date (2026-11-10) and never DateTime.Now. Cover duplicates in one batch, a scan in the future, and a final status.` |
| Result | Copilot added 1 test to `ParcelServiceMockTests.cs`: `UploadScans_DeliveredScanReachesFinalStatus_RejectsSubsequentScan` (+29 lines). The suite went from 174 to 175 passing tests. |
| Kept | Yes, unchanged. It reused the existing helpers (`ParcelExists`, `AlreadyStored`, `StagingWorks`, `Scan`) and the fixed `Now`; no `DateTime.Now`. |
| Review notes | It asserts real results (`Accepted == 1`, one rejection with the right scan id and a reason that names "Delivered") and the mock calls (only the first scan is stored, status moves `OutForDelivery` to `Delivered`, one commit). It partly overlaps the existing test just above it, which also checks that a scan after a final status is rejected. I kept it because it adds the "only the accepted scan is stored and committed once" check. |

Copilot covered only the final-status case. Duplicates in one batch and the future-scan case already existed in my own earlier tests, so Copilot's choice not to repeat them was reasonable.

### Round 2: status change and delete

| Item | Entry |
|---|---|
| Prompt | `@workspace /tests Write xUnit tests with Moq for ParcelService.UpdateStatusAsync and ParcelService.DeleteAsync in ApexPost.Application/Services/ParcelService.cs. Use Arrange/Act/Assert and fixed dates only.` |
| What Copilot did first | It stopped and asked a question: the service has `ChangeStatusAsync`, not `UpdateStatusAsync`. My prompt used a wrong method name and Copilot caught it. I chose "Test existing ChangeStatusAsync" and did not rename any code. |
| Result | Two new tests: `Delete_CreatedParcel_DeletesAndCommitsOnce` and `Delete_NonCreatedParcel_ThrowsConflictAndDoesNotPersist`. The suite went to 177. |

### Review of every generated test

| Test | Verdict | Reason |
|---|---|---|
| `UploadScans_DeliveredScanReachesFinalStatus_RejectsSubsequentScan` | Keep | Real rule, real assertions, can fail (proof below). Partly overlaps an older test. |
| `Delete_CreatedParcel_DeletesAndCommitsOnce` | Keep | Only a Created shipment may be deleted; checks one delete and one commit. It verifies mock calls only (no returned value to check), which is acceptable for a command that returns nothing. |
| `Delete_NonCreatedParcel_ThrowsConflictAndDoesNotPersist` | Keep after repair | The rule is right (a shipment that is already PickedUp cannot be deleted; nothing is saved). The generated code was damaged, see below. |

### What I rejected or repaired

1. **Rejected: a corrupted edit in `Delete_NonCreatedParcel_...`.** Copilot's final edit left three lines twice: `// Assert`, `Assert.Contains("Created", exception.Message);` and an unfinished `_parcels.Verify(repository => repository.DeleteAsync(`. A message "177 passed" had appeared before that last edit, so the green message could not be trusted for the final text. I removed the duplicate block and re-ran the whole suite: 177 passed, 0 failed.
2. **Rejected: a misleading comment change.** Copilot renamed the section comment from `CreateAsync and ChangeStatusAsync` to `ChangeStatusAsync and DeleteAsync`, but the section still contains the `Create_...` tests. I changed it to `CreateAsync, ChangeStatusAsync and DeleteAsync`.

Lesson: a generated test is a draft. "Build succeeded" and "tests passed" in the chat window describe an earlier state; only my own `dotnet test` run on the final file counts.

### Proof that a kept test can fail

I broke the production code on purpose in `ParcelService.cs`: the final-status check `if (ParcelTransitions.IsFinal(status))` was changed to `if (ParcelTransitions.IsFinal(status) && status == (ParcelStatus)(-1))`, so it never fires. Result:

```
Failed ...ParcelServiceMockTests.UploadScans_DeliveredScanReachesFinalStatus_RejectsSubsequentScan
Assert.Equal() Failure: Values differ
Expected: 1
Actual:   2
```

The scan after the Delivered scan was wrongly accepted, and the test caught it. The code was restored with `git checkout` (no change left in `git status`).
My first attempt, `if (false)`, did not even build: the project treats the "unreachable code" warning as an error. That is why the mutation had to be written so that it compiles.

### More proof that generated tests can fail (scripted)

`scripts/prove-tests-can-fail.ps1` breaks the production code on purpose, runs only the matching tests, expects them to FAIL, and restores the code. Besides the manual proof above, it proves three more things; the result of every mutation is in `docs/week5/mutation-proof.log`:

| Mutation (production code) | Copilot-assisted test that must fail |
|---|---|
| Final-status check in `UploadScansAsync` never fires | `UploadScans_DeliveredScanReachesFinalStatus_RejectsSubsequentScan` |
| `DeleteAsync` also allows a `PickedUp` shipment | `Delete_NonCreatedParcel_ThrowsConflictAndDoesNotPersist` |
| `ChangeStatusAsync` no longer saves the new status | the Copilot-written BDD scenarios in `ParcelStatusTracking.feature` |

## Task B: Copilot writes a Gherkin feature and its steps

| Item | Entry |
|---|---|
| Prompt | `Write a Gherkin feature "Parcel status tracking" for the ApexPost API with a Background, one Scenario Outline with Examples, and business wording only (no HTTP verbs, no JSON). Then write Reqnroll step definitions that call the API.` |
| What Copilot produced | `ParcelStatusTracking.feature` (one Background, one Scenario Outline with 4 example rows: PickedUp; then InTransit; then OutForDelivery; then Delivered) and `ParcelStatusTrackingSteps.cs`. The Specs project went from 11 to 15 passing scenarios. |
| What was good | Business wording only (no HTTP verbs, no JSON). It reused my existing steps for the Background (`the API is running...`, `signed in as "admin"`, `a shipment ... already exists`), so nothing was duplicated. The steps call the real `PATCH /api/v2/shipments/{id}/status` and then read the shipment back with `GET`, so the Then step checks real state, not a mock. |
| What was weak or wrong | The Examples table and the Background had mixed indentation (tabs and spaces). The `When` step uses `EnsureSuccessStatusCode()`, so an invalid status move would fail with an HTTP exception instead of a readable assertion. There is no negative case: a shipment cannot move backwards. |
| What I changed | Tidied the indentation of the feature file. I left the step code as Copilot wrote it, because it works and reads the real result. |
| What I would add next | A second outline for forbidden moves (for example Delivered then PickedUp must be refused with HTTP 409). |

## Rules I followed

1. Copilot gives a draft. I read every assertion before I kept it.
2. No secrets, no real names and no connection strings were pasted into a prompt.
3. Every kept test was run on the final file, and one was broken on purpose to see it fail.
4. Generated text that was wrong or damaged was rejected or repaired, and the reason is written above.
