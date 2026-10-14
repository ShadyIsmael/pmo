import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../i18n/messages";
import { NotificationsPage } from "../features/notifications/NotificationsPage";

const mocks = vi.hoisted(() => ({
  useNotifications: vi.fn(),
  useMarkNotificationRead: vi.fn(),
  useMarkAllNotificationsRead: vi.fn(),
  isFirebaseMessagingConfigured: vi.fn(),
  markRead: vi.fn(),
  markAll: vi.fn(),
}));

vi.mock("@api/notifications", () => ({
  useNotifications: mocks.useNotifications,
  useMarkNotificationRead: mocks.useMarkNotificationRead,
  useMarkAllNotificationsRead: mocks.useMarkAllNotificationsRead,
}));

vi.mock("@lib/firebase-messaging", () => ({
  isFirebaseMessagingConfigured: mocks.isFirebaseMessagingConfigured,
}));

describe("notification center real-time parity", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    mocks.useNotifications.mockReset().mockReturnValue({
      data: { items: [{ id: "notification-1", title: "Plan assigned", body: "Review the new plan", sentAt: "2026-09-20T10:00:00Z", isRead: false }], page: 1, pageSize: 20, totalCount: 1, totalPages: 1 },
      isLoading: false,
      isError: false,
    });
    mocks.markRead.mockReset();
    mocks.markAll.mockReset();
    mocks.useMarkNotificationRead.mockReset().mockReturnValue({ mutate: mocks.markRead, isPending: false, isError: false });
    mocks.useMarkAllNotificationsRead.mockReset().mockReturnValue({ mutate: mocks.markAll, isPending: false, isError: false });
    mocks.isFirebaseMessagingConfigured.mockReturnValue(true);
  });

  it("loads a paged notification center and exposes mark-read and mark-all actions", async () => {
    render(<MemoryRouter><I18nProvider><NotificationsPage /></I18nProvider></MemoryRouter>);

    expect(mocks.useNotifications).toHaveBeenCalledWith({ page: 1, pageSize: 20, unreadOnly: false }, true, true);
    expect(screen.getByText("Plan assigned")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Mark as read" }));
    expect(mocks.markRead).toHaveBeenCalledWith("notification-1");
    fireEvent.click(screen.getByRole("button", { name: "Mark all as read" }));
    expect(mocks.markAll).toHaveBeenCalledTimes(1);
    fireEvent.click(screen.getByLabelText("Unread only"));
    expect(mocks.useNotifications).toHaveBeenLastCalledWith({ page: 1, pageSize: 20, unreadOnly: true }, true, true);
  });

  it("keeps the preferences link and missing Firebase hint localized in Arabic", () => {
    window.localStorage.setItem("nexus-locale", "ar");
    mocks.isFirebaseMessagingConfigured.mockReturnValue(false);
    render(<MemoryRouter><I18nProvider><NotificationsPage /></I18nProvider></MemoryRouter>);

    expect(screen.getByRole("link", { name: "إدارة تفضيلات التسليم" })).toBeInTheDocument();
    expect(screen.getByText(/تتوفر الإشعارات عبر المتصفح بعد إضافة إعدادات Firebase/)).toBeInTheDocument();
  });
});
