import js from "@eslint/js";

export default [
  {
    ignores: ["**/node_modules/**", "**/bin/**", "**/obj/**", "**/.git/**"],
  },
  js.configs.recommended,
];
