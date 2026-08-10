import js from "@eslint/js";

export default [
  {
    ignores: ["**/node_modules/**", "**/bin/**", "**/obj/**", "**/.git/**", "docs/design/**"],
  },
  js.configs.recommended,
];
