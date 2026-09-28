"use client";

import * as React from "react";
import { Check, ChevronDown, Mail, Search, UserRound, X } from "lucide-react";
import { useTranslations } from "next-intl";
import { z } from "zod";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { useUserLookup, type UserLookupDto } from "@/hooks/projects/useProjectLookups";
import { cn } from "@/lib/utils";

export interface MeetingAttendeeSelection {
  userId?: string;
  email: string;
  displayName?: string;
}

interface MeetingUserPickerProps {
  id: string;
  label: string;
  value?: string;
  selectedUsers?: readonly UserLookupDto[];
  attendees?: readonly MeetingAttendeeSelection[];
  multiple?: boolean;
  onChange?: (value: string | undefined) => void;
  onAttendeesChange?: (attendees: MeetingAttendeeSelection[]) => void;
  required?: boolean;
  error?: string;
}

const emailSchema = z.string().trim().email();

export function MeetingUserPicker({ id, label, value, selectedUsers = [], attendees = [], multiple = false, onChange, onAttendeesChange, required = false, error }: MeetingUserPickerProps) {
  const t = useTranslations("meetings");
  const [open, setOpen] = React.useState(false);
  const [search, setSearch] = React.useState("");
  const { data: users, isLoading } = useUserLookup(undefined, open);
  const allUsers = React.useMemo(() => {
    const byId = new Map<string, UserLookupDto>();
    [...selectedUsers, ...(users ?? [])].forEach((user) => byId.set(user.id, user));
    return [...byId.values()];
  }, [selectedUsers, users]);
  const filteredUsers = React.useMemo(() => {
    const normalized = search.trim().toLocaleLowerCase();
    if (!normalized) return allUsers;
    return allUsers.filter((user) => `${user.fullName} ${user.email} ${user.jobTitle ?? ""}`.toLocaleLowerCase().includes(normalized));
  }, [allUsers, search]);
  const selectedUser = value ? allUsers.find((user) => user.id === value) : undefined;
  const externalEmail = search.trim();
  const canAddExternal = multiple && emailSchema.safeParse(externalEmail).success && !attendees.some((attendee) => attendee.email.trim().toLocaleLowerCase() === externalEmail.toLocaleLowerCase());
  const matchingInternalUser = canAddExternal && allUsers.some((user) => user.email.trim().toLocaleLowerCase() === externalEmail.toLocaleLowerCase());

  function toggleInternalUser(user: UserLookupDto) {
    if (!multiple) {
      onChange?.(user.id);
      setOpen(false);
      setSearch("");
      return;
    }
    const exists = attendees.some((attendee) => attendee.userId === user.id);
    onAttendeesChange?.(exists ? attendees.filter((attendee) => attendee.userId !== user.id) : [...attendees, { userId: user.id, email: user.email, displayName: user.fullName }]);
  }

  function addExternalAttendee() {
    if (!canAddExternal || matchingInternalUser) return;
    onAttendeesChange?.([...attendees, { email: externalEmail }]);
    setSearch("");
  }

  function removeAttendee(attendee: MeetingAttendeeSelection) {
    onAttendeesChange?.(attendees.filter((candidate) => attendeeKey(candidate) !== attendeeKey(attendee)));
  }

  function updateDisplayName(attendee: MeetingAttendeeSelection, displayName: string) {
    onAttendeesChange?.(attendees.map((candidate) => attendeeKey(candidate) === attendeeKey(attendee) ? { ...candidate, displayName } : candidate));
  }

  return (
    <div className="space-y-1.5">
      <label htmlFor={`${id}-trigger`} className="text-sm font-medium">{label}{required ? <span className="ms-1 text-destructive">*</span> : null}</label>
      <Popover open={open} onOpenChange={setOpen}>
        <PopoverTrigger asChild>
          <Button id={`${id}-trigger`} type="button" variant="outline" role="combobox" aria-expanded={open} aria-invalid={Boolean(error)} className={cn("w-full justify-between font-normal text-start", error && "border-destructive")}>
            <span className="truncate">{multiple ? attendees.length ? t("selectedUsers", { count: attendees.length }) : t("selectAttendees") : selectedUser?.fullName ?? t("selectOrganizer")}</span>
            <ChevronDown className="ms-2 h-4 w-4 shrink-0 opacity-50" />
          </Button>
        </PopoverTrigger>
        <PopoverContent align="start" className="w-[var(--radix-popover-trigger-width)] min-w-72 p-0">
          <div className="flex items-center border-b px-3"><Search className="h-4 w-4 shrink-0 text-muted-foreground" /><Input value={search} onChange={(event) => setSearch(event.target.value)} placeholder={t("searchUsers")} aria-label={t("searchUsers")} className="h-10 border-0 px-2 shadow-none focus-visible:ring-0" /></div>
          <div className="max-h-64 overflow-y-auto p-1" role="listbox" aria-label={label} aria-multiselectable={multiple}>
            {isLoading ? <p className="px-3 py-3 text-sm text-muted-foreground">{t("loadingUsers")}</p> : filteredUsers.length === 0 ? <p className="px-3 py-3 text-sm text-muted-foreground">{t("noUsers")}</p> : filteredUsers.map((user) => {
              const selected = multiple ? attendees.some((attendee) => attendee.userId === user.id) : user.id === value;
              return <button key={user.id} type="button" role="option" aria-selected={selected} onClick={() => toggleInternalUser(user)} className={cn("flex w-full items-center justify-between rounded-sm px-3 py-2 text-start text-sm hover:bg-accent", selected && "bg-accent/60")}><span className="min-w-0"><span className="block truncate font-medium">{user.fullName}</span><span className="block truncate text-xs text-muted-foreground">{user.email}</span><span className="mt-1 inline-flex items-center gap-1 text-[10px] text-muted-foreground"><UserRound className="h-3 w-3" />{t("internalUser")}</span></span>{selected ? <Check className="ms-2 h-4 w-4 shrink-0" /> : null}</button>;
            })}
            {canAddExternal && !matchingInternalUser ? <button type="button" onClick={addExternalAttendee} className="mt-1 flex w-full items-center gap-2 rounded-sm border-t px-3 py-3 text-start text-sm hover:bg-accent"><Mail className="h-4 w-4 shrink-0 text-muted-foreground" /><span className="min-w-0"><span className="block truncate font-medium">{t("addExternalAttendee", { email: externalEmail })}</span><span className="mt-1 block text-xs text-muted-foreground">{t("externalAttendee")}</span></span></button> : null}
          </div>
        </PopoverContent>
      </Popover>
      {multiple && attendees.length > 0 ? <div className="space-y-2">{attendees.map((attendee) => <div key={attendeeKey(attendee)} className="flex items-start gap-2 rounded-md border bg-muted/20 px-2.5 py-2"><div className="mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-muted">{attendee.userId ? <UserRound className="h-3.5 w-3.5 text-muted-foreground" /> : <Mail className="h-3.5 w-3.5 text-muted-foreground" />}</div><div className="min-w-0 flex-1"><div className="flex flex-wrap items-center gap-1.5"><span className="truncate text-xs font-medium">{attendee.displayName?.trim() || attendee.email}</span><Badge variant="outline" className="text-[9px]">{attendee.userId ? t("internalUser") : t("externalAttendee")}</Badge></div><p className="truncate text-xs text-muted-foreground">{attendee.email}</p>{!attendee.userId ? <Input value={attendee.displayName ?? ""} onChange={(event) => updateDisplayName(attendee, event.target.value)} placeholder={t("optionalDisplayName")} maxLength={200} className="mt-1.5 h-7 text-xs" aria-label={t("optionalDisplayName")} /> : null}</div><button type="button" onClick={() => removeAttendee(attendee)} aria-label={t("removeUser", { name: attendee.displayName?.trim() || attendee.email })} className="mt-1 rounded p-1 text-muted-foreground hover:bg-accent hover:text-foreground"><X className="h-3.5 w-3.5" /></button></div>)}</div> : null}
      {error ? <p className="text-xs text-destructive">{error}</p> : null}
    </div>
  );
}

function attendeeKey(attendee: MeetingAttendeeSelection) {
  return attendee.userId ? `user:${attendee.userId}` : `email:${attendee.email.trim().toLocaleLowerCase()}`;
}
