# ECSA 2026 Open Science Track Submission Checklist

**Artifact:** A Lock Free, Non-blocking, In Line Processing Architecture for High Throughput and Regulatory Compliant Blockchain Trading Applications

**Submission Deadline:** Friday, July 26, 2026 (TODAY)

**Submission URL:** https://ae.nfdixcs.org/ecsa2026

---

## Pre-Submission QA

### ✓ Documentation Complete
- [x] Dockerfile (multi-stage, .NET 10)
- [x] docker-compose.yml (all 5 architectures)
- [x] docker/README.md (quick-start guide)
- [x] docs/ARTIFACT_README.md (reproduction guide)
- [x] docs/REFERENCE_VALIDATION_REPORT.md (citation audit)
- [x] results/RESULTS_SUMMARY.md (data validation vs. paper)
- [x] open-science-submission/artifact-abstract.tex (2-page LaTeX)

### ✓ Code & Artifacts
- [x] Dockerfile includes all 5 architecture projects
- [x] docker-compose.yml orchestrates individual runs
- [x] BlockchainArchitecturePerformanceTesting/ (C# .NET 10)
- [x] smart_contract/ (Solidity + Hardhat)
- [x] images/ (performance graphs)
- [x] LICENSE (MIT)

### ✓ Quality Gates
- [x] **AI Disclosure:** Transparency statement included in abstract
- [x] **De-LLM'd:** Abstract humanized (signpost reduction, varied sentence structure)
- [x] **References Validated:** CrossRef audit complete; Herlihy1990 DOI added
- [x] **Data Fidelity:** Results summary table matches published Table 1 metrics
- [x] **Docker Test:** Artifact ready for `docker-compose run arch5-symbol-sharded`

---

## Archive Instructions

**Option 1: Use Git Archive (Recommended)**
```powershell
cd c:\dev\blockchain-trading-architectures
git archive --format zip --output blockchain-trading-architectures-zenodo.zip HEAD
```

**Option 2: Manual Zip (Windows)**
1. Right-click folder `c:\dev\blockchain-trading-architectures`
2. Select "Send to > Compressed (zipped) folder"
3. Rename to `blockchain-trading-architectures-zenodo.zip`

**Option 3: Command-line (PowerShell 7+)**
```powershell
cd c:\dev\blockchain-trading-architectures
Compress-Archive -Path @("Dockerfile", "docker-compose.yml", "docker", "open-science-submission", "docs", "results", "BlockchainArchitecturePerformanceTesting", "smart_contract", "images", "LICENSE", "README.md", ".gitignore") -DestinationPath "blockchain-trading-architectures-zenodo.zip" -Force
```

---

## Submission Steps

### Step 1: Create Zenodo Account & New Deposition
- Go to https://zenodo.org (or your institutional Zenodo instance)
- Sign in or create account
- Click "New Upload" (or "Create New Deposition")
- Select "Open Source" as upload type

### Step 2: Upload Artifact Archive
- **Title:** A Lock Free, Non-blocking, In Line Processing Architecture for High Throughput and Regulatory Compliant Blockchain Trading Applications  
- **Description:**
  ```
  Artifact submission for ECSA 2026 Open Science Track.
  
  Complete reference implementations of five blockchain transaction processing 
  architectures (C# .NET 10 + Solidity) with performance benchmarks. Includes 
  Docker containerization for reproducible evaluation and 2-page artifact abstract 
  describing methodology and results validation.
  
  All code, tests, and documentation are in the repository.
  Reproduction: docker-compose run arch5-symbol-sharded
  ```
- **Authors:** Andrew Le Gear, Jim Buckley, Tawny Whatmore, Ashish Sai
- **Upload file:** `blockchain-trading-architectures-zenodo.zip`
- **License:** MIT
- **Subjects:** Computer Science, Software Engineering, Blockchain, Performance Analysis

### Step 3: Add Metadata
- **Related Identifier:** Link to ECSA 2026 paper (if available)
- **Creator/Contributor:** Mark all authors as creators
- **Upload Type:** Software
- **Publication Date:** July 26, 2026

### Step 4: Publish & Get DOI
- Click "Publish"
- Zenodo generates a persistent DOI (e.g., `10.5281/zenodo.XXXXXXX`)
- Copy DOI

### Step 5: Submit to ECSA Open Science Track
- Go to https://ae.nfdixcs.org/ecsa2026
- Select "Open Science Track"
- Enter submission form:
  - **Artifact Title:** A Lock Free, Non-blocking, In Line Processing Architecture...
  - **Zenodo DOI:** `10.5281/zenodo.XXXXXXX` (from Step 4)
  - **Abstract:** Paste content from `open-science-submission/artifact-abstract.tex`
  - **Contact Email:** your email
- Submit by **23:59 AoE, Friday, July 26, 2026**

---

## Expected Zenodo Package Contents

```
blockchain-trading-architectures-zenodo.zip
├── Dockerfile                          # Multi-stage .NET 10 build
├── docker-compose.yml                  # Orchestration for all 5 architectures
├── docker/
│   └── README.md                       # Quick-start guide
├── open-science-submission/
│   └── artifact-abstract.tex           # 2-page submission abstract
├── docs/
│   ├── ARTIFACT_README.md              # Full reproduction guide
│   └── REFERENCE_VALIDATION_REPORT.md  # Citation audit results
├── results/
│   └── RESULTS_SUMMARY.md              # Data validation vs. paper
├── BlockchainArchitecturePerformanceTesting/
│   ├── SequentialBlockingSingleThreaded/
│   ├── SequentialAsyncSingleWallet/
│   ├── MultiThreadedAsyncSingleWallet/
│   ├── UnsynchronizedMultiThreadedMultiWallet/
│   ├── SymbolShardedLockFreeMultiWallet/
│   └── BlockchainArchitecturePerformanceTesting.slnx
├── smart_contract/
│   ├── contracts/
│   ├── scripts/
│   ├── hardhat.config.js
│   └── package.json
├── images/
│   ├── stat1.png
│   ├── stat2.png
│   └── stat3.png
├── LICENSE                             # MIT License
├── README.md                           # Project overview
└── .gitignore
```

---

## Verification Before Submission

Run this to ensure artifact works:
```bash
cd blockchain-trading-architectures
docker-compose build
docker-compose run arch5-symbol-sharded
```

**Expected output:**
- Throughput: ~31.19 tx/sec (±5%)
- Execution time: ~32 seconds (±10%)
- Output file: `results/arch5-results.txt` and `results/arch5-summary.json`

---

## Post-Submission

1. **Zenodo:** Record DOI (e.g., 10.5281/zenodo.XXXXXXX)
2. **Update Paper:** Add DOI to Acknowledgements or Data Availability section of published paper
3. **GitHub:** Optionally update repo README with Zenodo DOI and link

---

## Support Contacts

- **Zenodo Help:** support@zenodo.org
- **ECSA 2026 Artifacts:** [conference-artifacts-email]
- **Author Support:** andrew.legear@ul.ie

---

## Final Checklist (Before Clicking "Submit")

- [ ] Zenodo deposition created and published
- [ ] Zenodo DOI obtained (format: 10.5281/zenodo.XXXXXXX)
- [ ] Artifact archive is properly formatted and all files present
- [ ] Submission form filled out completely (title, abstract, DOI, authors)
- [ ] Submission deadline: Friday, July 26, 2026 23:59 AoE
- [ ] Submission URL: https://ae.nfdixcs.org/ecsa2026

---

**✓ ARTIFACT READY FOR SUBMISSION**

All phases complete:
- ✓ Phase 1: Docker containerization
- ✓ Phase 2: Packaging & documentation
- ✓ Phase 3: LaTeX abstract with AI disclosure + De-LLM'd + references validated
- ✓ Phase 4: QA & final validation

**Next action:** Create archive (Option 1-3 above) → Upload to Zenodo → Submit form
