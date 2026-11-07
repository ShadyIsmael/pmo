declare module "pdfmake-rtl/build/pdfmake.min.js" {
  interface PdfDocument {
    getBlob(): Promise<Blob>;
    print(): void;
  }

  interface PdfMake {
    addVirtualFileSystem(vfs: unknown): void;
    addFonts(fonts: Record<string, unknown>): void;
    createPdf(documentDefinition: unknown): PdfDocument;
  }

  const pdfMake: PdfMake;
  export default pdfMake;
}

declare module "pdfmake-rtl/build/vfs_fonts.js" {
  const vfs: unknown;
  export default vfs;
}
