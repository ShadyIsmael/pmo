import { createElement } from "react";
import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { MemoryRouter } from "react-router-dom";
import { MeetingPublicLink, meetingPublicPath } from "@lib/meeting-navigation";

describe("Meeting browser navigation", () => {
  it("builds a route only from the explicit Meeting PublicId", () => {
    const publicId = "f5d9d74f-15aa-4c2c-9df3-a8141bb37b5d";
    expect(meetingPublicPath(publicId)).toBe(`/meetings/${publicId}`);
  });

  it.each([undefined, null, "", "00000000-0000-0000-0000-000000000000", "not-a-public-id"])(
    "fails closed when the Meeting PublicId is %s",
    (publicId) => expect(meetingPublicPath(publicId)).toBeNull(),
  );

  it("renders a Meeting label without a link when its PublicId is missing", () => {
    render(createElement(MemoryRouter, null,
      createElement(MeetingPublicLink, { publicId: null, children: "Legacy meeting" }),
    ));

    expect(screen.getByText("Legacy meeting")).toHaveAttribute("aria-disabled", "true");
    expect(screen.queryByRole("link")).not.toBeInTheDocument();
  });
});
