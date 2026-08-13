"use client";

import * as React from "react";

export function DocumentDirection({ locale }: { locale: string }) {
  React.useEffect(() => {
    document.documentElement.setAttribute("dir", locale === "ar" ? "rtl" : "ltr");
    document.documentElement.setAttribute("lang", locale);
  }, [locale]);

  return null;
}
