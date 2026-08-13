/* eslint-disable no-unused-vars */
declare module "pdfmake-rtl/build/pdfmake.min.js" {
  const pdfMake: {
    createPdf(docDefinition: unknown): {
      getBlob(): Promise<Blob>;
    };
    addVirtualFileSystem(vfs: Record<string, string>): void;
    addFonts(fonts: Record<string, unknown>): void;
  };
  export default pdfMake;
}

declare module "pdfmake-rtl/build/vfs_fonts.js" {
  const vfs: Record<string, string>;
  export default vfs;
}
