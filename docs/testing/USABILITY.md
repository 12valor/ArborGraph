# ArborGraph First-Time User Usability Test Protocol

**Document Identifier:** AG-UT-001  
**Target Release:** v1.0.0  
**Testing Methodology:** Empirical task-based human usability evaluation with think-aloud protocol  
**Target Participants:** 5 real end-users (ranging from non-technical home users to professional software developers)  
**Execution Context:** Clean machine, no prior instruction or training provided  

---

## 1. Evaluation Protocol & Guidelines

### Participant Briefing
*"You are testing a new Windows desktop application designed to help analyze storage usage, identify what is consuming disk space, and clean up unnecessary files. Please think aloud as you navigate: tell us what you are looking at, what you expect to happen when you click something, and any points that confuse you. We are testing the software, not you."*

### Recorded Metrics
1. **Task Completion:** `Pass (Independent)` / `Pass (Assisted)` / `Fail (Abandoned)`
2. **Time to Complete (TTC):** Seconds from task instruction to successful outcome.
3. **Confusion Points:** Observable hesitation, misclicks, or expressed uncertainty.
4. **Error Rate:** Unintended actions performed (e.g. clicking wrong button, navigating wrong view).
5. **Subjective Satisfaction:** Post-test Likert scale (1 to 5).

---

## 2. Standardized User Tasks

### Task 1: Start a Drive Scan
- **Instruction:** *"Open the application and begin analyzing your main drive to see where your disk space is going."*
- **Optimal Path:** Accept EULA -> Click `Start Scan` button in top toolbar (or Overview view).
- **Success Criteria:** Scanner initiates; active Scanner view renders live throughput and directory feed.
- **Evaluation Form:**
  - Completion Status: `[ ] Independent  [ ] Assisted  [ ] Abandoned`
  - Time to Complete: `_____ seconds`
  - Confusion Points Observed: 
  - User Comments:

---

### Task 2: Find the Largest Folder on the Drive
- **Instruction:** *"Find out which single directory is taking up the most storage on your computer."*
- **Optimal Path:** Click `Files` -> `Largest Folders` (or click `Largest Folders` in sidebar / Overview drilldown).
- **Success Criteria:** User correctly reads the top-ranked folder name and formatted size.
- **Evaluation Form:**
  - Completion Status: `[ ] Independent  [ ] Assisted  [ ] Abandoned`
  - Time to Complete: `_____ seconds`
  - Confusion Points Observed: 
  - User Comments:

---

### Task 3: Find the Largest Individual File
- **Instruction:** *"Identify the single largest file stored on the disk."*
- **Optimal Path:** Click `Files` -> `Largest Files` sub-tab.
- **Success Criteria:** User names the top file listed at index 0 of the grid and its size.
- **Evaluation Form:**
  - Completion Status: `[ ] Independent  [ ] Assisted  [ ] Abandoned`
  - Time to Complete: `_____ seconds`
  - Confusion Points Observed: 
  - User Comments:

---

### Task 4: Understand and Interact with the Visual Treemap
- **Instruction:** *"Go to the Treemap. Explain what the colored blocks represent, and open one of the large blocks to see what is inside it."*
- **Optimal Path:** Click `Treemap` in sidebar -> Double-click a large folder rectangle (or click `Drill Down`).
- **Success Criteria:** User understands that rectangle area is proportional to byte size, colors map to categories, and double-clicking drills into that directory.
- **Evaluation Form:**
  - Completion Status: `[ ] Independent  [ ] Assisted  [ ] Abandoned`
  - Time to Complete: `_____ seconds`
  - Confusion Points Observed: 
  - User Comments:

---

### Task 5: Locate Duplicate Files
- **Instruction:** *"Find out if you have identical copies of files wasting disk space."*
- **Optimal Path:** Click `Duplicates` in sidebar -> Click `Scan for Duplicates` -> View duplicate groups.
- **Success Criteria:** User locates duplicate candidate list and identifies how much space could be reclaimed.
- **Evaluation Form:**
  - Completion Status: `[ ] Independent  [ ] Assisted  [ ] Abandoned`
  - Time to Complete: `_____ seconds`
  - Confusion Points Observed: 
  - User Comments:

---

### Task 6: Find Reclaimable Storage Opportunities
- **Instruction:** *"Find a quick summary of how much total disk space you could safely clean up right now."*
- **Optimal Path:** Look at Overview `Reclaimable Storage` KPI card or navigate to `Cleanup` tab.
- **Success Criteria:** User accurately states the estimated reclaimable gigabytes.
- **Evaluation Form:**
  - Completion Status: `[ ] Independent  [ ] Assisted  [ ] Abandoned`
  - Time to Complete: `_____ seconds`
  - Confusion Points Observed: 
  - User Comments:

---

### Task 7: Review Cleanup Center Opportunities
- **Instruction:** *"Go to the Cleanup Center and review the categories of files suggested for removal."*
- **Optimal Path:** Click `Cleanup` in sidebar -> Select category cards (Developer Junk, Adobe Scratch, Old Files, System Temp).
- **Success Criteria:** User reviews items in the right-hand inspection table.
- **Evaluation Form:**
  - Completion Status: `[ ] Independent  [ ] Assisted  [ ] Abandoned`
  - Time to Complete: `_____ seconds`
  - Confusion Points Observed: 
  - User Comments:

---

### Task 8: Understand What is Safe vs. Risky to Remove
- **Instruction:** *"Explain which items are completely safe to delete, and whether deleted files can be restored if you make a mistake."*
- **Optimal Path:** Inspect `SafetyBadge` in Cleanup Center (e.g. `Safe to Clean`) and note `Recycle Bin` option vs `Permanent Delete`.
- **Success Criteria:** User expresses confidence that default cleanup uses the Windows Recycle Bin and understands safety badges.
- **Evaluation Form:**
  - Completion Status: `[ ] Independent  [ ] Assisted  [ ] Abandoned`
  - Time to Complete: `_____ seconds`
  - Confusion Points Observed: 
  - User Comments:

---

## 3. Post-Test Usability Questionnaire (System Usability Scale)

Participants rate each statement from 1 (Strongly Disagree) to 5 (Strongly Agree):

1. I think that I would like to use ArborGraph frequently. `[ ]`
2. I found the software unnecessarily complex. `[ ]`
3. I thought the software was easy to use. `[ ]`
4. I think that I would need the support of a technical person to be able to use this software. `[ ]`
5. I found the various functions in this software were well integrated. `[ ]`
6. I thought there was too much inconsistency in this software. `[ ]`
7. I would imagine that most people would learn to use this software very quickly. `[ ]`
8. I found the software very cumbersome to use. `[ ]`
9. I felt very confident using the software. `[ ]`
10. I needed to learn a lot of things before I could get going with this software. `[ ]`

**Target Benchmark:** Overall SUS Score >= 80 (Grade A: Excellent usability).
