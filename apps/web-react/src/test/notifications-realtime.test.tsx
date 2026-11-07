import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../i18n/messages";
import { NotificationsPage } from "../features/notifications/NotificationsPage";

const meetingPublicId = "f5d9d74f-15aa-4c2c-9df3-a8141bb37b5d";
const commentPublicId = "a69b24b6-f0c2-4d10-8b60-5bc9f07a6c0e";
const replyPublicId = "5eeaa771-8681-4f8a-b1f7-60c857e23360";

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

const notificationItems = [
  {
    id: "notification-1",
    type: "MeetingInvitation",
    title: "Meeting Invitation\nدعوة لحضور اجتماع",
    body: "You are invited to \"Planning review\" on November 6 at 09:00 by Alex.\n\nتمت دعوتك لحضور «مراجعة التخطيط» يوم 6 نوفمبر الساعة 09:00 بواسطة أليكس.",
    referenceType: "Meeting",
    referenceId: meetingPublicId,
    sentAt: "2026-11-06T06:00:00Z",
    isRead: false,
  },
  {
    id: "notification-2",
    type: "MeetingReminder",
    title: "Upcoming Meeting\nتذكير باجتماع قادم",
    body: "Planning review starts November 6 at 09:00.\n\nيبدأ اجتماع «مراجعة التخطيط» يوم 6 نوفمبر الساعة 09:00.",
    referenceType: "Meeting",
    referenceId: meetingPublicId,
    sentAt: "2026-11-06T06:01:00Z",
    isRead: true,
  },
  {
    id: "notification-3",
    type: "MeetingStarted",
    title: "Meeting Started\nبدأ الاجتماع",
    body: "The meeting \"Planning review\" has started. You can open the meeting to follow its agenda and updates.\n\nبدأ الاجتماع «مراجعة التخطيط». يمكنك فتح الاجتماع لمتابعة جدول الأعمال والمستجدات.",
    referenceType: "Meeting",
    referenceId: meetingPublicId,
    sentAt: "2026-11-06T06:02:00Z",
    isRead: true,
  },
  {
    id: "notification-4",
    type: "MeetingMinutesPublished",
    title: "Meeting Minutes Available\nمحضر الاجتماع متاح",
    body: "The minutes for meeting \"Planning review\" have been published and are available for review.\n\nتم نشر محضر الاجتماع «مراجعة التخطيط» وأصبح متاحًا للمراجعة.",
    referenceType: "Meeting",
    referenceId: meetingPublicId,
    sentAt: "2026-11-06T06:03:00Z",
    isRead: true,
  },
  {
    id: "notification-5",
    type: "MeetingMinutesComment",
    title: "New Comment on Meeting Minutes\nتعليق جديد على محضر الاجتماع",
    body: "A new comment was added to the minutes of meeting \"Planning review\".\n\nتمت إضافة تعليق جديد على محضر الاجتماع «مراجعة التخطيط».",
    referenceType: "Meeting",
    referenceId: meetingPublicId,
    referencePublicId: commentPublicId,
    sentAt: "2026-11-06T06:04:00Z",
    isRead: true,
  },
  {
    id: "notification-6",
    type: "MeetingMinutesCommentReply",
    title: "New Reply on Meeting Minutes\nرد جديد على تعليق بمحضر الاجتماع",
    body: "A reply was added to a comment on the minutes of meeting \"Planning review\".\n\nتمت إضافة رد على تعليق بمحضر الاجتماع «مراجعة التخطيط».",
    referenceType: "Meeting",
    referenceId: meetingPublicId,
    referencePublicId: replyPublicId,
    sentAt: "2026-11-06T06:05:00Z",
    isRead: true,
  },
];

const expectedLinks = [
  `/meetings/${meetingPublicId}`,
  `/meetings/${meetingPublicId}`,
  `/meetings/${meetingPublicId}`,
  `/meetings/${meetingPublicId}?tab=minutes`,
  `/meetings/${meetingPublicId}?tab=minutes&comment=${commentPublicId}`,
  `/meetings/${meetingPublicId}?tab=minutes&reply=${replyPublicId}`,
];

function renderNotificationsPage() {
  return render(<MemoryRouter><I18nProvider><NotificationsPage /></I18nProvider></MemoryRouter>);
}

describe("notification center meeting events", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    mocks.useNotifications.mockReset().mockReturnValue({
      data: { items: notificationItems, page: 1, pageSize: 20, totalCount: 6, totalPages: 1 },
      isLoading: false,
      isError: false,
    });
    mocks.markRead.mockReset();
    mocks.markAll.mockReset();
    mocks.useMarkNotificationRead.mockReset().mockReturnValue({ mutate: mocks.markRead, isPending: false, isError: false });
    mocks.useMarkAllNotificationsRead.mockReset().mockReturnValue({ mutate: mocks.markAll, isPending: false, isError: false });
    mocks.isFirebaseMessagingConfigured.mockReturnValue(true);
  });

  it("renders all six English notifications, their public deep links, and read controls", () => {
    renderNotificationsPage();

    expect(mocks.useNotifications).toHaveBeenCalledWith({ page: 1, pageSize: 20, unreadOnly: false }, true, true);
    const titles = [
      "Meeting Invitation",
      "Upcoming Meeting",
      "Meeting Started",
      "Meeting Minutes Available",
      "New Comment on Meeting Minutes",
      "New Reply on Meeting Minutes",
    ];
    titles.forEach((title, index) => expect(screen.getByRole("link", { name: title })).toHaveAttribute("href", expectedLinks[index]));
    expect(screen.getByText(/You are invited to/)).toBeInTheDocument();
    expect(screen.getByText(/starts November 6 at 09:00/)).toBeInTheDocument();
    expect(screen.getByText(/has started/)).toBeInTheDocument();
    expect(screen.getByText(/have been published/)).toBeInTheDocument();
    expect(screen.getByText(/A new comment was added/)).toBeInTheDocument();
    expect(screen.getByText(/A reply was added/)).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Mark as read" }));
    expect(mocks.markRead).toHaveBeenCalledWith("notification-1");
    fireEvent.click(screen.getByRole("button", { name: "Mark all as read" }));
    expect(mocks.markAll).toHaveBeenCalledTimes(1);
    fireEvent.click(screen.getByLabelText("Unread only"));
    expect(mocks.useNotifications).toHaveBeenLastCalledWith({ page: 1, pageSize: 20, unreadOnly: true }, true, true);
  });

  it("renders all six Arabic notifications with RTL and preserves the same deep links", () => {
    window.localStorage.setItem("nexus-locale", "ar");
    renderNotificationsPage();

    expect(document.documentElement.dir).toBe("rtl");
    const titles = [
      "دعوة لحضور اجتماع",
      "تذكير باجتماع قادم",
      "بدأ الاجتماع",
      "محضر الاجتماع متاح",
      "تعليق جديد على محضر الاجتماع",
      "رد جديد على تعليق بمحضر الاجتماع",
    ];
    titles.forEach((title, index) => expect(screen.getByRole("link", { name: title })).toHaveAttribute("href", expectedLinks[index]));
    expect(screen.getByText(/تمت دعوتك لحضور/)).toBeInTheDocument();
    expect(screen.getByText(/يبدأ اجتماع/)).toBeInTheDocument();
    expect(screen.getByText(/يمكنك فتح الاجتماع/)).toBeInTheDocument();
    expect(screen.getByText(/تم نشر محضر الاجتماع/)).toBeInTheDocument();
    expect(screen.getByText(/تمت إضافة تعليق جديد/)).toBeInTheDocument();
    expect(screen.getByText(/تمت إضافة رد على تعليق/)).toBeInTheDocument();
  });

  it("keeps preferences and the missing Firebase hint localized in Arabic", () => {
    window.localStorage.setItem("nexus-locale", "ar");
    mocks.isFirebaseMessagingConfigured.mockReturnValue(false);
    renderNotificationsPage();

    expect(screen.getByRole("link", { name: "إدارة تفضيلات التسليم" })).toBeInTheDocument();
    expect(screen.getByText(/تتوفر الإشعارات عبر المتصفح بعد إضافة إعدادات Firebase/)).toBeInTheDocument();
  });
});
