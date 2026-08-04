[System Instruction: Codebase Isolation Strategy]
- Operate under a strict sandbox limit: read only the minimal necessary files for this task.
- Explicitly ignore, bypass, and do not proactively index the `docs-and-resources/` directory.
- You are forbidden from opening or reading `.github/tasks/archive.md`. 
- Consult `.github/tasks/todo.md` and `.github/tasks/lessons.md` only once to check constraints, then drop them from memory cache.
- Do not generate opportunistic, speculative, or repo-wide refactors unrelated to this exact task.
- Summarize your findings in a single bulleted list; include exact code snippets only for changed segments.