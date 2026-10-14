import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "@i18n/messages";
import { meetingsApiFull, type ChecklistItemDto, type MeetingAttachmentDto, type MeetingCommentDto } from "@api/meetingsFull";
import { MeetingMinutesCollaboration } from "@features/delivery/MeetingMinutesCollaboration";

let storedComments: MeetingCommentDto[] = [];

function renderPanel(canManage: boolean) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } } });
  return render(<QueryClientProvider client={client}><I18nProvider><MeetingMinutesCollaboration meetingId="meeting-1" canManage={canManage} locale="en" mainContent={<section>Minutes editor</section>} /></I18nProvider></QueryClientProvider>);
}

afterEach(() => {
  vi.restoreAllMocks();
  storedComments = [];
});

describe("meeting minutes collaboration", () => {
  it("creates a root comment and a nested reply, and keeps replies collapsed until requested", async () => {
    vi.spyOn(meetingsApiFull, "checklist").mockResolvedValue([]);
    vi.spyOn(meetingsApiFull, "attachments").mockResolvedValue([]);
    vi.spyOn(meetingsApiFull, "comments").mockImplementation(async () => storedComments);
    vi.spyOn(meetingsApiFull, "addComment").mockImplementation(async (_meetingId, body) => {
      const comment: MeetingCommentDto = {
        id: `comment-${storedComments.length + 1}`,
        meetingMinutesId: "minutes-1",
        parentCommentId: body.parentCommentId,
        commentText: body.commentText,
        createdBy: "user-1",
        authorName: "Alex Admin",
        createdAt: "2026-10-07T08:00:00Z",
      };
      storedComments = [...storedComments, comment];
      return comment;
    });

    renderPanel(true);
    fireEvent.change(screen.getByPlaceholderText("Write a comment..."), { target: { value: "Decision recorded" } });
    fireEvent.click(screen.getByRole("button", { name: "Add comment" }));
    expect(await screen.findByText("Decision recorded")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Reply" }));
    fireEvent.change(screen.getByPlaceholderText("Write a reply..."), { target: { value: "Follow-up noted" } });
    fireEvent.click(screen.getAllByRole("button", { name: "Reply" })[1]);
    await waitFor(() => expect(meetingsApiFull.addComment).toHaveBeenLastCalledWith("meeting-1", { commentText: "Follow-up noted", parentCommentId: "comment-1" }));
    expect(screen.queryByText("Follow-up noted")).not.toBeInTheDocument();

    fireEvent.click(await screen.findByRole("button", { name: /Show replies/ }));
    expect(await screen.findByText("Follow-up noted")).toBeInTheDocument();
  });

  it("does not expose collaboration mutations without Meeting.Manage", async () => {
    vi.spyOn(meetingsApiFull, "checklist").mockResolvedValue([]);
    vi.spyOn(meetingsApiFull, "attachments").mockResolvedValue([]);
    vi.spyOn(meetingsApiFull, "comments").mockResolvedValue([]);
    renderPanel(false);

    await screen.findByText("No follow-up items yet.");
    expect(screen.queryByPlaceholderText("Add a follow-up item...")).not.toBeInTheDocument();
    expect(screen.queryByPlaceholderText("Write a comment...")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Upload" })).not.toBeInTheDocument();
  });

  it("creates, completes, reorders, and deletes checklist items through the API", async () => {
    let checklist: ChecklistItemDto[] = [
      { id: "item-1", meetingMinutesId: "minutes-1", title: "Review scope", isCompleted: false, sortOrder: 0 },
      { id: "item-2", meetingMinutesId: "minutes-1", title: "Confirm owners", isCompleted: false, sortOrder: 1 },
    ];
    vi.spyOn(meetingsApiFull, "checklist").mockImplementation(async () => checklist);
    vi.spyOn(meetingsApiFull, "attachments").mockResolvedValue([]);
    vi.spyOn(meetingsApiFull, "comments").mockResolvedValue([]);
    vi.spyOn(meetingsApiFull, "createChecklist").mockImplementation(async (_meetingId, title) => {
      const item = { id: "item-3", meetingMinutesId: "minutes-1", title, isCompleted: false, sortOrder: checklist.length };
      checklist = [...checklist, item];
      return item;
    });
    vi.spyOn(meetingsApiFull, "updateChecklist").mockImplementation(async (_meetingId, itemId, body) => {
      checklist = checklist.map((item) => item.id === itemId ? { ...item, ...body } : item);
      return checklist.find((item) => item.id === itemId)!;
    });
    vi.spyOn(meetingsApiFull, "reorderChecklist").mockImplementation(async (_meetingId, itemIds) => {
      checklist = itemIds.map((id, sortOrder) => ({ ...checklist.find((item) => item.id === id)!, sortOrder }));
      return checklist;
    });
    vi.spyOn(meetingsApiFull, "deleteChecklist").mockImplementation(async (_meetingId, itemId) => {
      checklist = checklist.filter((item) => item.id !== itemId);
    });
    vi.spyOn(window, "confirm").mockReturnValue(true);

    renderPanel(true);
    await screen.findByText("Review scope");
    fireEvent.change(screen.getByPlaceholderText("Add a follow-up item..."), { target: { value: "Prepare deck" } });
    fireEvent.click(screen.getByRole("button", { name: "Add item" }));
    expect(await screen.findByText("Prepare deck")).toBeInTheDocument();
    expect(meetingsApiFull.createChecklist).toHaveBeenCalledWith("meeting-1", "Prepare deck");

    fireEvent.click(screen.getByRole("checkbox", { name: "Completed: Review scope" }));
    await waitFor(() => expect(meetingsApiFull.updateChecklist).toHaveBeenCalledWith("meeting-1", "item-1", { title: "Review scope", isCompleted: true }));

    fireEvent.click(screen.getAllByRole("button", { name: "Move up" })[1]);
    await waitFor(() => expect(meetingsApiFull.reorderChecklist).toHaveBeenCalledWith("meeting-1", ["item-2", "item-1", "item-3"]));

    fireEvent.click(screen.getAllByRole("button", { name: "Delete item" })[0]);
    await waitFor(() => expect(meetingsApiFull.deleteChecklist).toHaveBeenCalledWith("meeting-1", "item-2"));
    expect(window.confirm).toHaveBeenCalled();
  });

  it("uploads selected attachments and deletes an attachment with confirmation", async () => {
    let attachments: MeetingAttachmentDto[] = [
      { id: "attachment-1", meetingMinutesId: "minutes-1", fileName: "agenda.pdf", contentType: "application/pdf", fileSize: 2048, createdAt: "2026-10-07T08:00:00Z" },
    ];
    vi.spyOn(meetingsApiFull, "checklist").mockResolvedValue([]);
    vi.spyOn(meetingsApiFull, "attachments").mockImplementation(async () => attachments);
    vi.spyOn(meetingsApiFull, "comments").mockResolvedValue([]);
    vi.spyOn(meetingsApiFull, "uploadAttachments").mockImplementation(async (_meetingId, files) => {
      const uploaded = files.map((file, index) => ({ id: `uploaded-${index}`, meetingMinutesId: "minutes-1", fileName: file.name, contentType: file.type, fileSize: file.size, createdAt: "2026-10-07T08:00:00Z" }));
      attachments = [...attachments, ...uploaded];
      return uploaded;
    });
    vi.spyOn(meetingsApiFull, "deleteAttachment").mockImplementation(async (_meetingId, attachmentId) => {
      attachments = attachments.filter((attachment) => attachment.id !== attachmentId);
    });
    vi.spyOn(window, "confirm").mockReturnValue(true);

    const { container } = renderPanel(true);
    await screen.findByText("agenda.pdf");
    const uploadFile = new File(["minutes"], "minutes.txt", { type: "text/plain" });
    const fileInput = container.querySelector<HTMLInputElement>('input[type="file"]');
    expect(fileInput).not.toBeNull();
    fireEvent.change(fileInput!, { target: { files: [uploadFile] } });

    expect(await screen.findByText("minutes.txt")).toBeInTheDocument();
    expect(meetingsApiFull.uploadAttachments).toHaveBeenCalledWith("meeting-1", [uploadFile]);

    fireEvent.click(screen.getByRole("button", { name: "Delete attachment: agenda.pdf" }));
    await waitFor(() => expect(meetingsApiFull.deleteAttachment).toHaveBeenCalledWith("meeting-1", "attachment-1"));
    expect(window.confirm).toHaveBeenCalled();
  });
});
