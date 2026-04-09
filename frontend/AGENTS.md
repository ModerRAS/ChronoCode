# FRONTEND AGENTS

**Stack:** Vue 3, TypeScript, Vite, Ant Design Vue 4.x, Vue Router, Zod
**Testing:** Vitest (unit), Playwright (e2e)
**Dev Server:** http://localhost:5173

## STRUCTURE

```
frontend/
├── src/
│   ├── views/          # Route-level page components
│   ├── components/     # Reusable UI components
│   ├── api/             # HTTP client functions
│   ├── composables/     # Vue composition utilities
│   └── utils/           # Pure helper functions
├── tests/
│   ├── unit/            # Vitest component/spec tests
│   └── e2e/             # Playwright integration tests
└── package.json
```

## WHERE TO LOOK

| Task | Location |
|------|----------|
| Add new page | src/views/ + router |
| Create component | src/components/ |
| API calls | src/api/ |
| Shared logic | src/composables/ |
| Type guards/schemas | src/utils/ |
| Test components | tests/unit/*.spec.ts |

## CONVENTIONS

- **Strict TypeScript** - tsconfig has strict:true, noUnusedLocals:true, noUnusedParameters:true
- **Composable pattern** - shared logic via src/composables/
- **Zod schemas** - runtime validation for API responses
- **Ant Design Vue 4.x** - UI component library
- **Vue Router** - client-side routing

## COMMANDS

```bash
npm run dev      # Start dev server (localhost:5173)
npm run build    # Production build
npm run test     # Vitest unit tests
npm run test:e2e # Playwright e2e tests
```
