import type { CSSProperties, ReactNode } from "react";
import { Link } from "react-router-dom";

const PUBLIC_GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const EMPTY_GUID = "00000000-0000-0000-0000-000000000000";

/** Builds a browser route only from the Meeting's explicit public identifier. */
export function meetingPublicPath(publicId?: string | null): string | null {
  if (!publicId || publicId.toLowerCase() === EMPTY_GUID || !PUBLIC_GUID_PATTERN.test(publicId)) return null;
  return `/meetings/${encodeURIComponent(publicId)}`;
}

interface MeetingPublicLinkProps {
  publicId?: string | null;
  className?: string;
  title?: string;
  style?: CSSProperties;
  children: ReactNode;
}

/** Renders a non-navigable label if a Meeting PublicId is missing or invalid. */
export function MeetingPublicLink({ publicId, className, title, style, children }: MeetingPublicLinkProps) {
  const to = meetingPublicPath(publicId);
  return to
    ? <Link to={to} className={className} title={title} style={style}>{children}</Link>
    : <span aria-disabled="true" className={className} title={title} style={style}>{children}</span>;
}
