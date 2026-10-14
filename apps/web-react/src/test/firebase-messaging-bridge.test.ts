import { afterEach, describe, expect, it, vi } from "vitest";
import { subscribeToForegroundMessages } from "../lib/firebase-messaging";

describe("service-worker foreground notification bridge", () => {
  const originalDescriptor = Object.getOwnPropertyDescriptor(navigator, "serviceWorker");

  afterEach(() => {
    if (originalDescriptor) Object.defineProperty(navigator, "serviceWorker", originalDescriptor);
    else Reflect.deleteProperty(navigator, "serviceWorker");
  });

  it("forwards FCM payloads to the authenticated app bridge and unsubscribes cleanly", () => {
    const serviceWorker = new EventTarget();
    Object.defineProperty(navigator, "serviceWorker", { configurable: true, value: serviceWorker });
    const onMessage = vi.fn();
    const unsubscribe = subscribeToForegroundMessages(onMessage);
    const payload = { notification: { title: "Plan assigned", body: "Review the new plan" } };

    serviceWorker.dispatchEvent(new MessageEvent("message", { data: { type: "FCM_MESSAGE", payload } }));
    expect(onMessage).toHaveBeenCalledWith(payload);

    unsubscribe();
    serviceWorker.dispatchEvent(new MessageEvent("message", { data: { type: "FCM_MESSAGE", payload } }));
    expect(onMessage).toHaveBeenCalledTimes(1);
  });

  it("ignores unrelated service-worker messages", () => {
    const serviceWorker = new EventTarget();
    Object.defineProperty(navigator, "serviceWorker", { configurable: true, value: serviceWorker });
    const onMessage = vi.fn();
    const unsubscribe = subscribeToForegroundMessages(onMessage);

    serviceWorker.dispatchEvent(new MessageEvent("message", { data: { type: "OTHER_MESSAGE", payload: {} } }));
    expect(onMessage).not.toHaveBeenCalled();
    unsubscribe();
  });
});
