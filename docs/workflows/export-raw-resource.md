# Workflow: Exporting a Raw Resource

1. Add one or more source folders manually.
2. Run indexing.
3. Switch to `Raw Resource Browser`.
4. Search or filter by type, package path, TGI, or previewable/export-capable flags.
5. Select the resource to inspect metadata and preview. Scene-capable resource types may attempt the supported preview path, but raw export always writes package bytes rather than a reconstructed scene bundle.
6. Click `Raw Export` and choose an output folder.

The app writes exports under the chosen destination, preserves raw payload bytes from the package container, and never mutates the source package.
