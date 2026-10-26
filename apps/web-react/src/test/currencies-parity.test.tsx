import * as React from "react";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { CurrenciesPage } from "../features/administration/AdminPages";

const mocks = vi.hoisted(() => ({
  useAuth: vi.fn(),
  currencies: vi.fn(),
  createHook: vi.fn(),
  updateHook: vi.fn(),
  deleteHook: vi.fn(),
  create: vi.fn(),
  update: vi.fn(),
  remove: vi.fn(),
  refetch: vi.fn(),
}));

vi.mock("@auth/AuthProvider", () => ({ useAuth: mocks.useAuth }));
vi.mock("../features/administration/api/admin", () => ({
  useCurrencies: mocks.currencies,
  useCreateCurrency: mocks.createHook,
  useUpdateCurrency: mocks.updateHook,
  useDeleteCurrency: mocks.deleteHook,
}));

const initialCurrencies = [
  { id: "eur-id", code: "EUR", name: "Euro", symbol: "€", exchangeRate: 0.92, isBase: false },
  { id: "usd-id", code: "USD", name: "US Dollar", symbol: "$", exchangeRate: 1, isBase: true },
];

function renderPage(permissions = ["settings.read", "settings.write"]) {
  window.localStorage.setItem("nexus-locale", "en");
  mocks.useAuth.mockReturnValue({ user: { id: "admin-1", permissions }, status: "authenticated" });
  return render(<I18nProvider><MemoryRouter initialEntries={["/administration/currencies"]}><CurrenciesPage /></MemoryRouter></I18nProvider>);
}

describe("Currencies source parity", () => {
  beforeEach(() => {
    mocks.useAuth.mockReset().mockReturnValue({ user: { id: "admin-1", permissions: ["settings.read", "settings.write"] }, status: "authenticated" });
    mocks.currencies.mockReturnValue({ data: initialCurrencies, isLoading: false, isError: false, refetch: mocks.refetch });
    mocks.create.mockReset().mockResolvedValue(undefined);
    mocks.update.mockReset().mockResolvedValue(undefined);
    mocks.remove.mockReset().mockResolvedValue(undefined);
    mocks.refetch.mockReset();
    mocks.createHook.mockReturnValue({ mutateAsync: mocks.create, isPending: false });
    mocks.updateHook.mockReturnValue({ mutateAsync: mocks.update, isPending: false });
    mocks.deleteHook.mockReturnValue({ mutateAsync: mocks.remove, isPending: false });
  });

  it("lists currencies with the base currency first and hides management controls without Settings.Write", () => {
    renderPage(["settings.read"]);
    const currencyTitles = screen.getAllByText(/^[A-Z]{3} · /).map((element) => element.textContent);
    expect(currencyTitles).toEqual(["USD · US Dollar", "EUR · Euro"]);
    expect(screen.getByText("$ · Base currency")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Add Currency" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Edit Currency/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Delete/ })).not.toBeInTheDocument();
  });

  it("creates a normalized currency and preserves the inline source form flow", async () => {
    renderPage();
    fireEvent.click(screen.getByRole("button", { name: "Add Currency" }));
    expect(screen.getByText("Maintain the approved currency codes and symbols available to the platform.")).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText("Code"), { target: { value: "gbp" } });
    fireEvent.change(screen.getByLabelText("Name"), { target: { value: "  British Pound  " } });
    fireEvent.change(screen.getByLabelText("Symbol"), { target: { value: "  £  " } });
    fireEvent.click(screen.getByRole("checkbox", { name: "Base currency" }));
    fireEvent.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(mocks.create).toHaveBeenCalledWith({ code: "GBP", name: "British Pound", symbol: "£", isBase: true }));
    await waitFor(() => expect(screen.queryByText("Maintain the approved currency codes and symbols available to the platform.")).not.toBeInTheDocument());
  });

  it("edits currency code, name, symbol and base flag through the API", async () => {
    renderPage();
    const euro = screen.getByText("EUR · Euro").closest(".rounded-lg.border");
    if (!(euro instanceof HTMLElement)) throw new Error("Euro currency row was not rendered");
    fireEvent.click(within(euro).getByRole("button", { name: "Edit Currency" }));
    expect(screen.getByRole("heading", { name: "Edit Currency" })).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText("Code"), { target: { value: "gbp" } });
    fireEvent.change(screen.getByLabelText("Name"), { target: { value: "Pound Sterling" } });
    fireEvent.change(screen.getByLabelText("Symbol"), { target: { value: "£" } });
    fireEvent.click(screen.getByRole("checkbox", { name: "Base currency" }));
    fireEvent.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(mocks.update).toHaveBeenCalledWith({ id: "eur-id", input: { code: "GBP", name: "Pound Sterling", symbol: "£", isBase: true } }));
    await waitFor(() => expect(screen.queryByRole("heading", { name: "Edit Currency" })).not.toBeInTheDocument());
  });

  it("prevents base-currency deletion and confirms and reports errors for other currencies", async () => {
    mocks.remove.mockRejectedValue(new Error("Currency is still referenced"));
    renderPage();
    const baseDelete = screen.getByRole("button", { name: "Delete USD" });
    expect(baseDelete).toBeDisabled();
    fireEvent.click(baseDelete);
    expect(screen.queryByRole("alertdialog")).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Delete EUR" }));
    const confirmation = screen.getByRole("alertdialog", { name: "Confirm action" });
    expect(within(confirmation).getByText("Delete currency EUR?")).toBeInTheDocument();
    fireEvent.click(within(confirmation).getByRole("button", { name: "Confirm" }));
    await waitFor(() => expect(mocks.remove).toHaveBeenCalledWith("eur-id"));
    expect(await screen.findByRole("alert")).toHaveTextContent("Currency is still referenced");
  });

  it("allows retry after a currency query fails", () => {
    mocks.currencies.mockReturnValue({ data: undefined, isLoading: false, isError: true, refetch: mocks.refetch });
    renderPage();
    fireEvent.click(screen.getByRole("button", { name: "Retry" }));
    expect(mocks.refetch).toHaveBeenCalledOnce();
  });
});
