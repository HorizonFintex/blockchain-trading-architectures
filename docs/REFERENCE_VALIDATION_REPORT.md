# Reference Validation Report

**Artifact:** blockchain-trading-architectures  
**Date:** July 26, 2026  
**Tool:** CrossRef Academic Reference Checker

---

## Summary

Four references were validated against the CrossRef database. Three issues were identified as "HIGH" severity, but all are **expected and acceptable** in the context of this Open Science Track submission.

| Reference | Status | Notes |
|-----------|--------|-------|
| LeGear2026 | ✓ Expected | Self-citation; conference paper being supplemented |
| Herlihy1990 | ✓ Fixed | Added missing DOI: 10.1145/78969.78972 |
| Lamport1978 | ✓ Confirmed | Original publication correct; CrossRef has reprint |
| Wood2014 | ✓ Confirmed | Technical white paper (not in traditional indexes) |

---

## Detailed Findings

### 1. LeGear2026 — Self-Citation (HIGH - Expected)

**Reference:**
```
Le Gear, A., Buckley, J., Whatmore, T., & Sai, A. (2026). 
A Lock Free, Non-blocking, In Line Processing Architecture for High Throughput 
and Regulatory Compliant Blockchain Trading Applications. 
Proceedings of the European Software Architecture Conference (ECSA 2026).
```

**Finding:** No matching publication found in CrossRef.

**Explanation:** This reference is the published paper itself, which this artifact supplements. It will not be indexed in CrossRef until the conference proceedings are formally published (likely Q4 2026). Including the self-citation is appropriate and transparent.

**Resolution:** ✓ **No action needed.** Self-citations are standard in Open Science Track submissions.

---

### 2. Herlihy1990 — Linearizability Paper (LOW - Fixed)

**Reference:**
```
Herlihy, M., & Wing, J. M. (1990). 
Linearizability: A correctness condition for concurrent objects. 
ACM Transactions on Programming Languages and Systems, 12(3), 463--492.
```

**Finding:** Reference is correct; DOI was missing.

**Action Taken:** Added DOI — 10.1145/78969.78972

**Updated Reference:**
```
Herlihy, M., & Wing, J. M. (1990). 
Linearizability: A correctness condition for concurrent objects. 
ACM Transactions on Programming Languages and Systems, 12(3), 463--492. 
https://doi.org/10.1145/78969.78972
```

**Resolution:** ✓ **Fixed** — DOI now included for easier access.

---

### 3. Lamport1978 — Time, Clocks, Ordering (HIGH - Confirmed Correct)

**Reference:**
```
Lamport, L. (1978). Time, clocks, and the ordering of events in a distributed system. 
Communications of the ACM, 21(7), 558--565.
```

**Finding:** CrossRef has a year mismatch warning (2019) and journal name variation.

**Explanation:** This is Lamport's foundational 1978 paper. CrossRef also indexes a later reprint/collection (2019: "Concurrency: the Works of Leslie Lamport"). The original 1978 publication in *Communications of the ACM* is the correct citation for this work.

**Resolution:** ✓ **Confirmed correct** — Original 1978 publication is the appropriate reference. No change needed.

---

### 4. Wood2014 — Ethereum Yellow Paper (HIGH - Confirmed Correct)

**Reference:**
```
Wood, G. (2014). Ethereum: A secure decentralised generalised transaction ledger. 
Ethereum Yellow Paper.
```

**Finding:** No matching publication found in CrossRef.

**Explanation:** The Ethereum Yellow Paper is a technical white paper, not a peer-reviewed journal article or conference proceedings. It is not indexed in CrossRef (which focuses on traditional academic publications). The Yellow Paper is the definitive specification for Ethereum and is widely cited in blockchain research.

**Resolution:** ✓ **Confirmed correct** — Technical white papers are standard citations in blockchain/systems research. No change needed.

---

## Validation Checklist

- ✓ All references checked against CrossRef
- ✓ Herlihy1990 DOI added (LOW → Fixed)
- ✓ LeGear2026 self-citation appropriate (HIGH → Expected)
- ✓ Lamport1978 original publication confirmed (HIGH → Correct)
- ✓ Wood2014 technical paper confirmed (HIGH → Appropriate)
- ✓ No retracted publications detected
- ✓ No duplicate references detected

---

## Conclusion

All references are **valid and appropriate** for this artifact. The "HIGH" severity flags are context-dependent (self-citations, original vs. reprint, technical specifications) and do not indicate errors or misconduct. The artifact is **ready for submission**.

---

**Validation Status:** ✓ **PASSED**

Evaluated by: Reference Checker (CrossRef validation tool)  
Date: 2026-07-23
