# Implementation Plan: PhotoTransfer Console Application

**Branch**: `001-the-console-application` | **Date**: 2025-09-07 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `C:/Users/gnnko/ref-test/specs/001-the-console-application/spec.md`

## Execution Flow (/plan command scope)
```
1. Load feature spec from Input path
   → If not found: ERROR "No feature spec at {path}"
2. Fill Technical Context (scan for NEEDS CLARIFICATION)
   → Detect Project Type from context (web=frontend+backend, mobile=app+api)
   → Set Structure Decision based on project type
3. Evaluate Constitution Check section below
   → If violations exist: Document in Complexity Tracking
   → If no justification possible: ERROR "Simplify approach first"
   → Update Progress Tracking: Initial Constitution Check
4. Execute Phase 0 → research.md
   → If NEEDS CLARIFICATION remain: ERROR "Resolve unknowns"
5. Execute Phase 1 → contracts, data-model.md, quickstart.md, agent-specific template file (e.g., `CLAUDE.md` for Claude Code, `.github/copilot-instructions.md` for GitHub Copilot, or `GEMINI.md` for Gemini CLI).
6. Re-evaluate Constitution Check section
   → If new violations: Refactor design, return to Phase 1
   → Update Progress Tracking: Post-Design Constitution Check
7. Plan Phase 2 → Describe task generation approach (DO NOT create tasks.md)
8. STOP - Ready for /tasks command
```

**IMPORTANT**: The /plan command STOPS at step 7. Phases 2-4 are executed by other commands:
- Phase 2: /tasks command creates tasks.md
- Phase 3-4: Implementation execution (manual or via tools)

## Summary
PhotoTransfer is a console application that provides two-phase photo organization: (1) indexing all photos from current directory and subdirectories by creation date into a local metadata file, and (2) transferring photos from specific time periods (YYYY-MM format) to organized directories. The application will be built using .NET 9 with minimal libraries, implementing duplicate handling via numeric suffixes.

## Technical Context
**Language/Version**: C# .NET 9  
**Primary Dependencies**: Minimal - System.Text.Json for metadata, System.IO for file operations, System.CommandLine for CLI  
**Storage**: JSON metadata file stored locally, file system operations for photo transfer  
**Testing**: NUnit for unit testing, integration tests for file operations  
**Target Platform**: Cross-platform console application (Windows, Linux, macOS)
**Project Type**: single - console application with library structure  
**Performance Goals**: Handle thousands of photos efficiently, metadata operations <1sec for typical collections  
**Constraints**: Minimal memory footprint, no external dependencies, atomic file operations to prevent data loss  
**Scale/Scope**: Support photo collections up to 100k files, handle common image formats (jpg, jpeg, png, gif, bmp, tiff)
**Duplicate Handling**: Add numeric suffixes (0), (1), etc. when transferring files with same names  
**Progress Visualization**: Pulsating asterisk during indexing operations to show application is working

## Constitution Check
*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Simplicity**:
- Projects: 1 (phototransfer console app)
- Using framework directly? Yes - direct .NET 9 APIs, no wrapper classes
- Single data model? Yes - PhotoMetadata entity, JSON serialization matches model
- Avoiding patterns? Yes - direct file operations, no Repository/UoW patterns

**Architecture**:
- EVERY feature as library? Yes - PhotoIndexer, PhotoTransfer, MetadataStore libraries
- Libraries listed: PhotoIndexer (scan/index), PhotoTransfer (move operations), MetadataStore (JSON persistence)  
- CLI per library: phototransfer --index, phototransfer --YYYY-MM, --help/--version supported
- Library docs: llms.txt format planned for each library

**Testing (NON-NEGOTIABLE)**:
- RED-GREEN-Refactor cycle enforced? Yes - tests written first, must fail before implementation
- Git commits show tests before implementation? Yes - strict TDD workflow
- Order: Contract→Integration→E2E→Unit strictly followed? Yes
- Real dependencies used? Yes - actual file system, real directories for integration tests
- Integration tests for: new libraries, file operations, metadata persistence, CLI commands
- FORBIDDEN: Implementation before test, skipping RED phase

**Observability**:
- Structured logging included? Yes - structured console output with operation status and pulsating progress indicator
- Frontend logs → backend? N/A - console application
- Error context sufficient? Yes - detailed error messages with file paths and operation context

**Versioning**:
- Version number assigned? 1.0.0 (MAJOR.MINOR.BUILD)
- BUILD increments on every change? Yes
- Breaking changes handled? Yes - metadata format versioning, migration plan

## Project Structure

### Documentation (this feature)
```
specs/[###-feature]/
├── plan.md              # This file (/plan command output)
├── research.md          # Phase 0 output (/plan command)
├── data-model.md        # Phase 1 output (/plan command)
├── quickstart.md        # Phase 1 output (/plan command)
├── contracts/           # Phase 1 output (/plan command)
└── tasks.md             # Phase 2 output (/tasks command - NOT created by /plan)
```

### Source Code (repository root)
```
# Option 1: Single project (DEFAULT)
src/
├── models/
├── services/
├── cli/
└── lib/

tests/
├── contract/
├── integration/
└── unit/

# Option 2: Web application (when "frontend" + "backend" detected)
backend/
├── src/
│   ├── models/
│   ├── services/
│   └── api/
└── tests/

frontend/
├── src/
│   ├── components/
│   ├── pages/
│   └── services/
└── tests/

# Option 3: Mobile + API (when "iOS/Android" detected)
api/
└── [same as backend above]

ios/ or android/
└── [platform-specific structure]
```

**Structure Decision**: Option 1 (Single project) - Console application with library structure

## Phase 0: Outline & Research
1. **Extract unknowns from Technical Context** above:
   - For each NEEDS CLARIFICATION → research task
   - For each dependency → best practices task
   - For each integration → patterns task

2. **Generate and dispatch research agents**:
   ```
   For each unknown in Technical Context:
     Task: "Research {unknown} for {feature context}"
   For each technology choice:
     Task: "Find best practices for {tech} in {domain}"
   ```

3. **Consolidate findings** in `research.md` using format:
   - Decision: [what was chosen]
   - Rationale: [why chosen]
   - Alternatives considered: [what else evaluated]

**Output**: research.md with all NEEDS CLARIFICATION resolved

## Phase 1: Design & Contracts
*Prerequisites: research.md complete*

1. **Extract entities from feature spec** → `data-model.md`:
   - Entity name, fields, relationships
   - Validation rules from requirements
   - State transitions if applicable

2. **Generate API contracts** from functional requirements:
   - For each user action → endpoint
   - Use standard REST/GraphQL patterns
   - Output OpenAPI/GraphQL schema to `/contracts/`

3. **Generate contract tests** from contracts:
   - One test file per endpoint
   - Assert request/response schemas
   - Tests must fail (no implementation yet)

4. **Extract test scenarios** from user stories:
   - Each story → integration test scenario
   - Quickstart test = story validation steps

5. **Update agent file incrementally** (O(1) operation):
   - Run `/scripts/update-agent-context.sh [claude|gemini|copilot]` for your AI assistant
   - If exists: Add only NEW tech from current plan
   - Preserve manual additions between markers
   - Update recent changes (keep last 3)
   - Keep under 150 lines for token efficiency
   - Output to repository root

**Output**: data-model.md, /contracts/*, failing tests, quickstart.md, agent-specific file

## Phase 2: Task Planning Approach
*This section describes what the /tasks command will do - DO NOT execute during /plan*

**Task Generation Strategy**:
- Load `/templates/tasks-template.md` as base
- Generate tasks from Phase 1 design docs (contracts, data model, quickstart)
- CLI contract → contract test tasks for --index and --YYYY-MM commands [P]
- Data entities → model creation tasks for PhotoMetadata, PhotoIndex, TransferOperation [P] 
- Each user story → integration test task for indexing and transfer workflows
- Library implementation tasks: PhotoIndexer, PhotoTransfer, MetadataStore libraries
- CLI implementation tasks to wire everything together

**Ordering Strategy**:
- TDD order: Contract tests → Integration tests → Unit tests → Implementation
- Dependency order: Models → Services → CLI → Integration 
- Mark [P] for parallel execution: model classes, library implementations
- Sequential: CLI integration, end-to-end testing

**PhotoTransfer-Specific Tasks**:
1. Contract tests for CLI interface (commands, options, exit codes)
2. Data model classes with validation
3. PhotoIndexer library (scan directories, extract EXIF data)
4. MetadataStore library (JSON persistence, versioning)
5. PhotoTransfer library (move operations, duplicate handling)
6. Progress visualization component (pulsating asterisk for long operations)
7. CLI implementation (System.CommandLine integration)
8. Integration tests (real file operations)
9. End-to-end quickstart validation

**Estimated Output**: 20-25 numbered, ordered tasks in tasks.md

**IMPORTANT**: This phase is executed by the /tasks command, NOT by /plan

## Phase 3+: Future Implementation
*These phases are beyond the scope of the /plan command*

**Phase 3**: Task execution (/tasks command creates tasks.md)  
**Phase 4**: Implementation (execute tasks.md following constitutional principles)  
**Phase 5**: Validation (run tests, execute quickstart.md, performance validation)

## Complexity Tracking
*Fill ONLY if Constitution Check has violations that must be justified*

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |


## Progress Tracking
*This checklist is updated during execution flow*

**Phase Status**:
- [x] Phase 0: Research complete (/plan command)
- [x] Phase 1: Design complete (/plan command)  
- [x] Phase 2: Task planning complete (/plan command - describe approach only)
- [ ] Phase 3: Tasks generated (/tasks command)
- [ ] Phase 4: Implementation complete
- [ ] Phase 5: Validation passed

**Gate Status**:
- [x] Initial Constitution Check: PASS
- [x] Post-Design Constitution Check: PASS
- [x] All NEEDS CLARIFICATION resolved
- [x] Complexity deviations documented (none required)

---
*Based on Constitution v2.1.1 - See `/memory/constitution.md`*