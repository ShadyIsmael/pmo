import en from "../messages/en.json";
import ar from "../messages/ar.json";

export const messages = { en, ar } as const;
export type MessageKey = keyof typeof en;
export type Locale = keyof typeof messages;
